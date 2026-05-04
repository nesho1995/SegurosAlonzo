using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;
using ReclamosWhatsApp.Services.DataQuality;

namespace ReclamosWhatsApp.Controllers;

[Authorize]
[Route("api/[controller]/[action]/{id?}")]
public class CarteraMutacionesController : ControllerBase
{
    private readonly CarteraRepository _cartera;
    private readonly AuditoriaService _auditoria;
    private readonly PhoneNormalizationService _phones;
    private readonly PolizaImportRulesService _historicalRules;

    public CarteraMutacionesController(CarteraRepository cartera, AuditoriaService auditoria, PhoneNormalizationService phones, PolizaImportRulesService historicalRules)
    {
        _cartera = cartera;
        _auditoria = auditoria;
        _phones = phones;
        _historicalRules = historicalRules;
    }

    [HttpPost]
    [Authorize(Policy = Permissions.ClientesCrear)]
    public async Task<IActionResult> NuevoCliente([FromBody] Cliente model)
    {
        model.Id = 0;
        model.Activo = true;

        if (string.IsNullOrWhiteSpace(model.Nombre))
            return BadRequest(new { error = "El nombre del cliente es requerido." });

        NormalizeClientPhones(model);
        var id = await _cartera.InsertClienteAsync(model);

        if (!string.IsNullOrWhiteSpace(model.Telefono))
            await _cartera.InsertTelefonoAsync(id, model.Telefono, true);
        if (!string.IsNullOrWhiteSpace(model.TelefonoSecundario))
            await _cartera.InsertTelefonoAsync(id, model.TelefonoSecundario, false);

        await _auditoria.RegistrarAsync("CREAR_CLIENTE", "CLIENTE", id, $"Cliente creado: {model.Nombre}");
        return Ok(new { id });
    }

    [HttpPut]
    [Authorize(Policy = Permissions.ClientesEditar)]
    public async Task<IActionResult> GuardarCliente(int id, [FromBody] Cliente model)
    {
        if (id != model.Id)
            return BadRequest(new { error = "El cliente no coincide con la ruta." });

        var existente = await _cartera.GetClienteByIdAsync(id);
        if (existente is null)
            return NotFound();

        NormalizeClientPhones(model);
        await _cartera.UpdateClienteAsync(model);

        if (!string.IsNullOrWhiteSpace(model.Telefono))
            await _cartera.InsertTelefonoAsync(model.Id, model.Telefono, true);
        if (!string.IsNullOrWhiteSpace(model.TelefonoSecundario))
            await _cartera.InsertTelefonoAsync(model.Id, model.TelefonoSecundario, false);

        await _auditoria.RegistrarAsync("EDITAR_CLIENTE", "CLIENTE", model.Id, $"Cliente actualizado: {model.Nombre}");
        if (ContactChanged(existente, model))
            await _auditoria.RegistrarAsync("EDITAR_CONTACTO_CLIENTE", "CLIENTE", model.Id, "Telefonos/correos del cliente actualizados.");
        return NoContent();
    }

    [HttpPatch]
    [Authorize(Policy = Permissions.ClientesEditar)]
    public async Task<IActionResult> CambiarEstadoCliente(int id, [FromBody] ClienteEstadoRequest request)
    {
        var existente = await _cartera.GetClienteByIdAsync(id);
        if (existente is null)
            return NotFound(new { error = "El cliente no existe." });

        existente.Activo = request.Activo;
        await _cartera.UpdateClienteAsync(existente);
        await _auditoria.RegistrarAsync(
            request.Activo ? "ACTIVAR_CLIENTE" : "INACTIVAR_CLIENTE",
            "CLIENTE",
            id,
            request.Activo ? "Cliente reactivado." : "Cliente desactivado logicamente.");

        return NoContent();
    }

    [HttpPatch]
    [Authorize(Policy = Permissions.ClientesEditar)]
    public async Task<IActionResult> MarcarClienteRevisado(int id)
    {
        var existente = await _cartera.GetClienteByIdAsync(id);
        if (existente is null)
            return NotFound(new { error = "El cliente no existe." });

        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : (int?)null;
        await _cartera.MarcarClienteRevisadoAsync(id, userId);
        await _auditoria.RegistrarAsync("MARCAR_CLIENTE_REVISADO", "CLIENTE", id, "Cliente y polizas marcados como revisados.");
        return NoContent();
    }

    [HttpPost]
    [Authorize(Policy = Permissions.PolizasCrear)]
    public async Task<IActionResult> NuevaPoliza(int id, [FromBody] Poliza model)
    {
        var cliente = await _cartera.GetClienteByIdAsync(id);
        if (cliente is null)
            return NotFound();

        model.Id = 0;
        model.ClienteId = id;
        model.Activo = true;

        if (string.IsNullOrWhiteSpace(model.EstadoPago))
            model.EstadoPago = "SIN_VALIDAR";

        SetManualOrigins(model);
        NormalizeSumaAsegurada(model);
        NormalizePolicyTextFields(model);
        await ResolveContractorAsync(model);
        model.VehiculoId = await _cartera.UpsertVehiculoAsync(BuildVehicleFromPolicy(model, id, "MANUAL"));
        var polizaId = await _cartera.InsertPolizaAsync(model);
        await EnsureCuotasForManualPolicyAsync(model, polizaId);
        await _auditoria.RegistrarAsync("CREAR_POLIZA", "POLIZA", polizaId, $"Poliza creada para cliente {id}.");
        return NoContent();
    }

    [HttpPut]
    [Authorize(Policy = Permissions.PolizasEditar)]
    public async Task<IActionResult> GuardarPoliza(int id, [FromBody] Poliza model)
    {
        if (id != model.Id)
            return BadRequest(new { error = "La poliza no coincide con la ruta." });

        var existente = await _cartera.GetPolizaByIdAsync(id);
        if (existente is null)
            return NotFound();

        SetManualOrigins(model);
        NormalizeSumaAsegurada(model);
        NormalizePolicyTextFields(model);
        await ResolveContractorAsync(model);
        model.VehiculoId = await _cartera.UpsertVehiculoAsync(BuildVehicleFromPolicy(model, model.ClienteId, "MANUAL")) ?? model.VehiculoId;
        await _cartera.UpdatePolizaAsync(model);
        await _auditoria.RegistrarAsync("EDITAR_POLIZA", "POLIZA", model.Id, $"Poliza actualizada: {model.NumeroPoliza ?? "Sin numero"}");
        return NoContent();
    }

    [HttpPatch]
    [Authorize(Policy = Permissions.PolizasEditar)]
    public async Task<IActionResult> CambiarEstadoPoliza(int id, [FromBody] PolizaEstadoRequest request)
    {
        var existente = await _cartera.GetPolizaByIdAsync(id);
        if (existente is null)
            return NotFound();

        await _cartera.SetPolizaActivaAsync(id, request.Activo);
        await _auditoria.RegistrarAsync(request.Activo ? "ACTIVAR_POLIZA" : "INACTIVAR_POLIZA", "POLIZA", id, request.Activo ? "Poliza activada." : "Poliza inactivada.");
        return NoContent();
    }

    [HttpPatch]
    [Authorize(Policy = Permissions.PolizasEditar)]
    public async Task<IActionResult> ReanalizarPolizaHistorica(int id)
    {
        var poliza = await _cartera.GetPolizaByIdAsync(id);
        if (poliza is null)
            return NotFound(new { error = "La poliza no existe." });

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

        await _auditoria.RegistrarAsync("REANALISIS_HISTORICO_POLIZA", "POLIZA", id, "Se reejecutaron reglas historicas sobre la poliza.");
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
        else if (!string.IsNullOrWhiteSpace(model.TelefonoSecundario))
        {
            model.Contacto = model.TelefonoSecundario;
        }
    }

    private static bool ContactChanged(Cliente before, Cliente after)
    {
        return !string.Equals(before.Telefono, after.Telefono, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(before.TelefonoSecundario ?? before.Contacto, after.TelefonoSecundario ?? after.Contacto, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(before.TelefonosExtraJson, after.TelefonosExtraJson, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(before.Email, after.Email, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(before.CorreosExtraJson, after.CorreosExtraJson, StringComparison.OrdinalIgnoreCase);
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

public class ClienteEstadoRequest
{
    public bool Activo { get; set; }
}
