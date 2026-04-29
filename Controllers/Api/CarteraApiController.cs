using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text.RegularExpressions;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;
using ReclamosWhatsApp.Services.DataQuality;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/cartera")]
public class CarteraApiController : ControllerBase
{
    private readonly CarteraRepository _cartera;
    private readonly AutomationEngineService _automation;
    private readonly PhoneNormalizationService _phones;
    private readonly PolizaImportRulesService _historicalRules;

    public CarteraApiController(CarteraRepository cartera, AutomationEngineService automation, PhoneNormalizationService phones, PolizaImportRulesService historicalRules)
    {
        _cartera = cartera;
        _automation = automation;
        _phones = phones;
        _historicalRules = historicalRules;
    }

    [HttpGet("clientes")]
    [Authorize(Policy = Permissions.ClientesVer)]
    public async Task<IActionResult> GetClientes(
        string? buscar = null,
        string? estado = "ACTIVO",
        string? financiera = null,
        string? aseguradora = null,
        string? ramo = null,
        string? estadoPago = null,
        string? ciudad = null,
        int pagina = 1,
        int pageSize = 25)
    {
        var (items, total) = await _cartera.GetClientesListadoAsync(
            buscar,
            estado,
            pagina,
            pageSize,
            financiera,
            aseguradora,
            ramo,
            estadoPago,
            ciudad);

        return Ok(new
        {
            items,
            total,
            pagina,
            pageSize,
            totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize))
        });
    }

    [HttpGet("clientes/{id:int}")]
    [Authorize(Policy = Permissions.ClientesVer)]
    public async Task<IActionResult> GetCliente(int id)
    {
        var cliente = await _cartera.GetClienteByIdAsync(id);

        if (cliente is null)
            return NotFound();

        return Ok(new ClienteDetalle
        {
            Cliente = cliente,
            Telefonos = await _cartera.GetTelefonosClienteAsync(id),
            Polizas = await _cartera.GetPolizasByClienteAsync(id)
        });
    }

    [HttpGet("polizas/{id:int}/cuotas")]
    [Authorize(Policy = Permissions.PagosVer)]
    public async Task<IActionResult> GetCuotasPoliza(int id)
    {
        var poliza = await _cartera.GetPolizaByIdAsync(id);
        if (poliza is null)
            return NotFound();

        var cuotas = (await _cartera.GetCuotasByPolizaAsync(id)).ToList();
        var totalCuotas = poliza.Cuotas.HasValue && poliza.Cuotas.Value > 0
            ? Math.Min(poliza.Cuotas.Value, 12)
            : Math.Min(Math.Max(cuotas.Count, 1), 12);
        var espacios = Enumerable.Range(1, totalCuotas)
            .Select(numero => cuotas.FirstOrDefault(x => x.NumeroCuota == numero) ?? new PolizaCuota
            {
                PolizaId = id,
                ClienteId = poliza.ClienteId,
                NumeroPoliza = poliza.NumeroPoliza,
                NumeroCuota = numero,
                Estado = "PENDIENTE"
            });

        return Ok(new { items = espacios });
    }

    [HttpPost("~/api/cartera-nuevo-cliente")]
    [Authorize(Policy = Permissions.ClientesCrear)]
    public async Task<IActionResult> CreateCliente(Cliente model)
    {
        model.Id = 0;
        model.Activo = true;

        if (string.IsNullOrWhiteSpace(model.Nombre))
            return BadRequest("El nombre del cliente es requerido.");

        NormalizeClientPhones(model);
        var id = await _cartera.InsertClienteAsync(model);

        if (!string.IsNullOrWhiteSpace(model.Telefono))
            await _cartera.InsertTelefonoAsync(id, model.Telefono, true);
        if (!string.IsNullOrWhiteSpace(model.TelefonoSecundario))
            await _cartera.InsertTelefonoAsync(id, model.TelefonoSecundario, false);

        await SafeAutomationAsync("cliente_creado", "CLIENTE", id, model);

        return CreatedAtAction(nameof(GetCliente), new { id }, new { id });
    }

    [HttpPut("~/api/cartera-guardar-cliente/{id:int}")]
    [Authorize(Policy = Permissions.ClientesEditar)]
    public async Task<IActionResult> UpdateCliente(int id, Cliente model)
    {
        if (id != model.Id)
            return BadRequest("El cliente no coincide con la ruta.");

        var existente = await _cartera.GetClienteByIdAsync(id);
        if (existente is null)
            return NotFound();

        NormalizeClientPhones(model);
        await _cartera.UpdateClienteAsync(model);

        if (!string.IsNullOrWhiteSpace(model.Telefono))
            await _cartera.InsertTelefonoAsync(model.Id, model.Telefono, true);
        if (!string.IsNullOrWhiteSpace(model.TelefonoSecundario))
            await _cartera.InsertTelefonoAsync(model.Id, model.TelefonoSecundario, false);

        return NoContent();
    }

    [HttpPut("~/api/cartera-guardar-poliza/{id:int}")]
    [Authorize(Policy = Permissions.PolizasEditar)]
    public async Task<IActionResult> UpdatePoliza(int id, Poliza model)
    {
        if (id != model.Id)
            return BadRequest("La poliza no coincide con la ruta.");

        var existente = await _cartera.GetPolizaByIdAsync(id);
        if (existente is null)
            return NotFound();

        SetManualOrigins(model);
        NormalizeSumaAsegurada(model);
        NormalizePolicyTextFields(model);
        await ResolveContractorAsync(model);
        model.VehiculoId = await _cartera.UpsertVehiculoAsync(BuildVehicleFromPolicy(model, model.ClienteId, "MANUAL")) ?? model.VehiculoId;
        await _cartera.UpdatePolizaAsync(model);
        return NoContent();
    }

    [HttpPost("~/api/cartera-nueva-poliza/{clienteId:int}")]
    [Authorize(Policy = Permissions.PolizasCrear)]
    public async Task<IActionResult> CreatePoliza(int clienteId, Poliza model)
    {
        var cliente = await _cartera.GetClienteByIdAsync(clienteId);
        if (cliente is null)
            return NotFound();

        model.Id = 0;
        model.ClienteId = clienteId;
        model.Activo = true;

        if (string.IsNullOrWhiteSpace(model.EstadoPago))
            model.EstadoPago = "SIN_VALIDAR";

        SetManualOrigins(model);
        NormalizeSumaAsegurada(model);
        NormalizePolicyTextFields(model);
        await ResolveContractorAsync(model);
        model.VehiculoId = await _cartera.UpsertVehiculoAsync(BuildVehicleFromPolicy(model, clienteId, "MANUAL"));
        var polizaId = await _cartera.InsertPolizaAsync(model);
        await EnsureCuotasForManualPolicyAsync(model, polizaId);
        return NoContent();
    }

    [HttpPatch("cuotas/{cuotaId:int}/monto")]
    [Authorize(Policy = Permissions.PagosEditar)]
    public async Task<IActionResult> UpdateCuotaMonto(int cuotaId, [FromBody] UpdateCuotaMontoRequest request)
    {
        if (request.Monto < 0)
            return BadRequest("El monto no puede ser negativo.");

        var updated = await _cartera.UpdateCuotaMontoAsync(cuotaId, request.Monto);
        return updated ? NoContent() : NotFound();
    }

    [HttpPatch("~/api/cartera-cambiar-estado-poliza/{id:int}")]
    [Authorize(Policy = Permissions.PolizasEditar)]
    public async Task<IActionResult> SetPolizaEstado(int id, [FromBody] PolizaEstadoRequest request)
    {
        var existente = await _cartera.GetPolizaByIdAsync(id);
        if (existente is null)
            return NotFound();

        await _cartera.SetPolizaActivaAsync(id, request.Activo);
        return NoContent();
    }

    [HttpPatch("polizas/{id:int}/reanalizar-historico")]
    [Authorize(Policy = Permissions.PolizasEditar)]
    public async Task<IActionResult> ReanalizarHistoricoPoliza(int id)
    {
        var poliza = await _cartera.GetPolizaByIdAsync(id);
        if (poliza is null)
            return NotFound();

        var cliente = await _cartera.GetClienteByIdAsync(poliza.ClienteId);
        _historicalRules.ApplyHistoricalRules(poliza, new HistoricalRulesContext
        {
            RamoRaw = poliza.Ramo,
            FormaPagoRaw = poliza.FormaPago,
            EmisionRenovacionRaw = poliza.EmisionRenovacion,
            Observacion2 = poliza.Observacion2,
            Observaciones = poliza.Observaciones,
            SumaAseguradaOriginal = poliza.SumaAseguradaTextoOriginal,
            SumaAseguradaLimpia = poliza.SumaAseguradaTextoOriginal,
            Cliente = cliente,
            AllowOverwriteAutomaticOnly = true
        });

        await _cartera.UpdatePolizaAsync(poliza);
        if (cliente is not null)
            await _cartera.UpdateClienteAsync(cliente);

        return NoContent();
    }

    private void NormalizeClientPhones(Cliente model)
    {
        var principal = _phones.NormalizeMany(model.Telefono);
        if (!string.IsNullOrWhiteSpace(principal.Principal))
            model.Telefono = principal.Principal;

        var secundarioRaw = model.TelefonoSecundario ?? model.Contacto;
        var secundario = _phones.NormalizeMany(secundarioRaw);
        if (!string.IsNullOrWhiteSpace(secundario.Principal))
        {
            model.TelefonoSecundario = secundario.Principal;
            model.Contacto = secundario.Principal;
        }
    }

    private async Task SafeAutomationAsync(string evento, string entidadTipo, int entidadId, object data)
    {
        try
        {
            await _automation.EvaluarEventoAsync(evento, entidadTipo, entidadId, data);
        }
        catch
        {
            // La automatizacion nunca debe romper el flujo principal del cliente.
        }
    }

    private static void NormalizeSumaAsegurada(Poliza model)
    {
        var raw = model.SumaAseguradaTextoOriginal;
        if (string.IsNullOrWhiteSpace(raw) && model.SumaAsegurada is not null)
            raw = model.SumaAsegurada.Value.ToString(CultureInfo.InvariantCulture);

        model.SumaAseguradaTextoOriginal = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        if (string.IsNullOrWhiteSpace(model.SumaAseguradaTextoOriginal))
            return;

        if (string.Equals(model.Ramo?.Trim(), "MEDICO", StringComparison.OrdinalIgnoreCase) && model.SumaAseguradaTextoOriginal.Contains('/'))
        {
            var parts = model.SumaAseguradaTextoOriginal
                .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseDecimal)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();
            if (parts.Count >= 2)
            {
                model.MaximoVitalicio = parts.Max();
                model.SumaAseguradaVida = parts.Min();
                model.SumaAsegurada = model.SumaAseguradaVida;
                return;
            }
        }

        var single = ParseDecimal(model.SumaAseguradaTextoOriginal);
        if (single.HasValue)
            model.SumaAsegurada = single.Value;
    }

    private static void SetManualOrigins(Poliza model)
    {
        model.OrigenRamoNormalizado = "MANUAL";
        model.OrigenEstadoPago = "MANUAL";
        model.OrigenSumaAsegurada = "MANUAL";
        if (!string.IsNullOrWhiteSpace(model.TipoProceso))
            model.OrigenTipoProceso = "MANUAL";
        if (!string.IsNullOrWhiteSpace(model.EstadoPolizaReal))
            model.OrigenEstadoPolizaReal = "MANUAL";
    }

    private static void NormalizePolicyTextFields(Poliza model)
    {
        model.NumeroItem = string.IsNullOrWhiteSpace(model.NumeroItem) ? null : model.NumeroItem.Trim();
        model.Certificado = string.IsNullOrWhiteSpace(model.Certificado) ? null : model.Certificado.Trim();
        model.Endoso = string.IsNullOrWhiteSpace(model.Endoso) ? null : model.Endoso.Trim();
    }

    private static decimal? ParseDecimal(string value)
    {
        var clean = Regex.Replace(value ?? "", @"[^\d\.,\-]", "").Replace(",", "");
        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static Vehiculo BuildVehicleFromPolicy(Poliza policy, int clienteId, string origen)
    {
        return new Vehiculo
        {
            ClienteId = clienteId,
            Marca = policy.Marca,
            Modelo = policy.Modelo,
            Anio = policy.Anio,
            Color = policy.Color,
            Tipo = policy.TipoVehiculo,
            Placa = policy.Placa,
            Motor = policy.Motor,
            VinSerie = policy.VinSerie,
            Chasis = policy.Chasis,
            OrigenDatos = origen
        };
    }

    private async Task EnsureCuotasForManualPolicyAsync(Poliza policy, int polizaId)
    {
        var cuotas = Math.Clamp(policy.Cuotas ?? 0, 0, 12);
        if (cuotas <= 0)
            return;

        var fechaBase = policy.Vigencia ?? policy.FechaInicio ?? DateTime.Today;
        var montos = Enumerable.Repeat<decimal?>(0m, 12).ToArray();
        await _cartera.UpsertCuotasAsync(polizaId, cuotas, fechaBase.Date, montos);
    }

    private async Task ResolveContractorAsync(Poliza policy)
    {
        var contractorName = policy.ClienteContratanteNombre?.Trim();
        if (string.IsNullOrWhiteSpace(contractorName))
            return;

        policy.ClienteContratanteId = await _cartera.GetOrCreateClienteAsync(contractorName);
        policy.ClienteContratanteNombre = contractorName;
    }
}

public class PolizaEstadoRequest
{
    public bool Activo { get; set; }
}

public class UpdateCuotaMontoRequest
{
    public decimal Monto { get; set; }
}
