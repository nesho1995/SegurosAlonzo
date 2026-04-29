using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize(Policy = Permissions.RecordatoriosVer)]
[Route("api/recordatorios")]
public class RecordatoriosApiController : ControllerBase
{
    private readonly RecordatorioRepository _recordatorios;
    private readonly AppSettingsRepository _settings;
    private readonly AutomaticWhatsAppService _automaticWhatsApp;
    private readonly AuditoriaService _auditoria;

    public RecordatoriosApiController(RecordatorioRepository recordatorios, AppSettingsRepository settings, AutomaticWhatsAppService automaticWhatsApp, AuditoriaService auditoria)
    {
        _recordatorios = recordatorios;
        _settings = settings;
        _automaticWhatsApp = automaticWhatsApp;
        _auditoria = auditoria;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] RecordatorioFiltro filtro)
    {
        var (items, total) = await _recordatorios.GetAsync(filtro);

        return Ok(new
        {
            items,
            total,
            totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)filtro.PageSize)),
            stats = await _recordatorios.GetStatsAsync(),
            tipos = await _recordatorios.GetTiposResumenAsync(filtro.Estado)
        });
    }

    [HttpPost("generar")]
    [Authorize(Policy = Permissions.RecordatoriosEnviar)]
    public async Task<IActionResult> Generar()
    {
        var creados = await _recordatorios.GenerarPendientesAsync(await _settings.GetEnvioAutomaticoConfigAsync());
        await _automaticWhatsApp.ProcesarRecordatoriosPendientesAsync();
        await _auditoria.RegistrarAsync("GENERAR_RECORDATORIOS", "RECORDATORIO", null, $"Recordatorios creados: {creados}.");
        return Ok(new { creados });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.RecordatoriosEnviar)]
    public async Task<IActionResult> Editar(int id, [FromBody] RecordatorioUpdateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Asunto) || string.IsNullOrWhiteSpace(request.Mensaje))
            return BadRequest(new { error = "El asunto y el mensaje son requeridos." });

        await _recordatorios.UpdateMensajeAsync(id, request.Asunto.Trim(), request.Mensaje.Trim());
        await _auditoria.RegistrarAsync("EDITAR_RECORDATORIO", "RECORDATORIO", id, "Recordatorio editado.");
        return NoContent();
    }

    [HttpPost("{id:int}/enviar")]
    [Authorize(Policy = Permissions.RecordatoriosEnviar)]
    public async Task<IActionResult> Enviar(int id)
    {
        var recordatorio = await _recordatorios.GetByIdAsync(id);
        if (recordatorio is null)
            return NotFound(new { error = "El recordatorio no existe." });

        var result = await _automaticWhatsApp.EnviarRecordatorioAsync(recordatorio, automatico: false);
        return Ok(new { result.ok, result.response });
    }

    [HttpPost("{id:int}/descartar")]
    [Authorize(Policy = Permissions.RecordatoriosEnviar)]
    public async Task<IActionResult> Descartar(int id)
    {
        await _recordatorios.MarcarDescartadoAsync(id);
        await _auditoria.RegistrarAsync("DESCARTAR_RECORDATORIO", "RECORDATORIO", id, "Recordatorio descartado.");
        return NoContent();
    }
}

public sealed record RecordatorioUpdateRequest(string Asunto, string Mensaje);
