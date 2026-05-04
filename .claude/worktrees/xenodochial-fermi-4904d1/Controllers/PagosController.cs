using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers;

[Authorize(Policy = Permissions.PagosVer)]
public class PagosController : Controller
{
    private readonly PagoRepository _pagos;
    private readonly AuditoriaService _auditoria;

    public PagosController(PagoRepository pagos, AuditoriaService auditoria)
    {
        _pagos = pagos;
        _auditoria = auditoria;
    }

    public async Task<IActionResult> Index(string? estado = "PENDIENTE", string? buscar = null, int pagina = 1, int pageSize = 25)
    {
        await _pagos.ActualizarEstadosVencidosAsync();
        var (cuotas, total) = await _pagos.GetCuotasAsync(estado, buscar, pagina, pageSize);

        var model = new PagoBandejaViewModel
        {
            Estado = estado,
            Buscar = buscar,
            Pagina = pagina < 1 ? 1 : pagina,
            PageSize = pageSize is < 10 or > 100 ? 25 : pageSize,
            Total = total,
            TotalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)(pageSize is < 10 or > 100 ? 25 : pageSize))),
            Cuotas = cuotas,
            Stats = await _pagos.GetStatsAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.PagosEditar)]
    public async Task<IActionResult> GenerarCuotas()
    {
        var creadas = await _pagos.GenerarCuotasAsync();
        await _auditoria.RegistrarAsync("GENERAR_CUOTAS", "PAGO", null, $"Cuotas generadas: {creadas}.");
        TempData["Mensaje"] = $"Cuotas generadas: {creadas}.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.PagosEditar)]
    public async Task<IActionResult> MarcarPagada(int id, DateTime? fechaPago, string? observaciones)
    {
        await _pagos.MarcarPagadaAsync(id, fechaPago ?? DateTime.Today, observaciones);
        await _auditoria.RegistrarAsync("MARCAR_CUOTA_PAGADA", "PAGO", id, "Cuota marcada como pagada.");
        TempData["Mensaje"] = "Cuota marcada como pagada.";

        return RedirectToAction(nameof(Index));
    }
}
