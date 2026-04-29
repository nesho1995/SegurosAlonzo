using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize(Policy = Permissions.PagosVer)]
[Route("api/pagos")]
public class PagosApiController : ControllerBase
{
    private readonly PagoRepository _pagos;
    private readonly AutomationEngineService _automation;
    private readonly AuditoriaService _auditoria;

    public PagosApiController(PagoRepository pagos, AutomationEngineService automation, AuditoriaService auditoria)
    {
        _pagos = pagos;
        _automation = automation;
        _auditoria = auditoria;
    }

    [HttpGet]
    public async Task<IActionResult> Get(string? estado = "PENDIENTE", string? buscar = null, int pagina = 1, int pageSize = 25)
    {
        await _pagos.ActualizarEstadosVencidosAsync();
        var (items, total) = await _pagos.GetCuotasAsync(estado, buscar, pagina, pageSize);
        foreach (var item in items.Where(x => string.Equals(x.Estado, "VENCIDA", StringComparison.OrdinalIgnoreCase)).Take(50))
            await SafeAutomationAsync("pago_vencido", "PAGO", item.Id, item);

        return Ok(new
        {
            items,
            total,
            pagina,
            pageSize,
            totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)),
            stats = await _pagos.GetStatsAsync()
        });
    }

    [HttpGet("cuotas/{cuotaId:int}/pagos")]
    [Authorize(Policy = Permissions.PagosVer)]
    public async Task<IActionResult> GetPagosByCuota(int cuotaId)
    {
        var items = await _pagos.GetPagosByCuotaAsync(cuotaId);
        return Ok(new { items });
    }

    [HttpPost("cuotas/{cuotaId:int}/registrar")]
    [Authorize(Policy = Permissions.PagosEditar)]
    public async Task<IActionResult> RegistrarPago(int cuotaId, [FromBody] RegistrarPagoRequest request)
    {
        if (request.Monto <= 0)
            return BadRequest(new { error = "El monto del pago debe ser mayor a cero." });

        var metodo = (request.MetodoPago ?? "").Trim().ToUpperInvariant();
        var requiereComprobante = metodo.Contains("TRANSFERENCIA", StringComparison.Ordinal)
                                  || metodo.Contains("DEBITO", StringComparison.Ordinal);
        if (requiereComprobante && request.DocumentoId is null)
            return BadRequest(new { error = "Para transferencia/debito se recomienda adjuntar comprobante." });

        var pagoId = await _pagos.RegistrarPagoAsync(cuotaId, new PolizaPago
        {
            CuotaId = cuotaId,
            Monto = request.Monto,
            FechaPago = request.FechaPago ?? DateTime.Today,
            MetodoPago = string.IsNullOrWhiteSpace(request.MetodoPago) ? "OTRO" : request.MetodoPago!.Trim().ToUpperInvariant(),
            DocumentoId = request.DocumentoId,
            NumeroRecibo = string.IsNullOrWhiteSpace(request.NumeroRecibo) ? null : request.NumeroRecibo.Trim(),
            ReferenciaBanco = string.IsNullOrWhiteSpace(request.ReferenciaBanco) ? null : request.ReferenciaBanco.Trim(),
            Observaciones = string.IsNullOrWhiteSpace(request.Observaciones) ? null : request.Observaciones.Trim(),
            RegistradoPorUsuarioId = GetUsuarioId()
        });

        await _auditoria.RegistrarAsync("REGISTRAR_PAGO_CUOTA", "PAGO", cuotaId, $"Pago parcial/total registrado. PagoId={pagoId}, monto={request.Monto}.");
        return Ok(new { pagoId });
    }

    private async Task SafeAutomationAsync(string evento, string entidadTipo, int entidadId, object data)
    {
        try
        {
            await _automation.EvaluarEventoAsync(evento, entidadTipo, entidadId, data);
        }
        catch
        {
            // La automatizacion no debe bloquear la bandeja de pagos.
        }
    }

    private int? GetUsuarioId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }
}
