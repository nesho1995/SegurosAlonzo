using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize(Policy = Permissions.AutomatizacionesVer)]
[Route("api/automatizaciones")]
public class AutomatizacionesApiController : ControllerBase
{
    private readonly AutomationRepository _repo;
    private readonly AutomationEngineService _engine;
    private readonly AuditoriaService _auditoria;

    public AutomatizacionesApiController(
        AutomationRepository repo,
        AutomationEngineService engine,
        AuditoriaService auditoria)
    {
        _repo = repo;
        _engine = engine;
        _auditoria = auditoria;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok(new
        {
            items = await _repo.GetAllAsync(),
            logs = await _repo.GetLogsAsync()
        });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.AutomatizacionesCrear)]
    public async Task<IActionResult> Create(AutomatizacionRequest request)
    {
        var error = ValidateRequest(request);
        if (error is not null)
            return BadRequest(new { error });

        var id = await _repo.InsertAsync(request);
        await _auditoria.RegistrarAsync("AUTOMATIZACION_CREADA", "AUTOMATIZACION", id, $"Regla creada: {request.Nombre.Trim()}.");
        return CreatedAtAction(nameof(Get), new { id }, new { id, message = "Regla creada correctamente." });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.AutomatizacionesEditar)]
    public async Task<IActionResult> Update(int id, AutomatizacionRequest request)
    {
        var error = ValidateRequest(request);
        if (error is not null)
            return BadRequest(new { error });

        var updated = await _repo.UpdateAsync(id, request);
        if (!updated)
            return NotFound(new { error = "No se encontro la regla de automatizacion." });

        await _auditoria.RegistrarAsync("AUTOMATIZACION_EDITADA", "AUTOMATIZACION", id, $"Regla editada: {request.Nombre.Trim()}.");
        return Ok(new { message = "Regla actualizada correctamente." });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.AutomatizacionesEliminar)]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _repo.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { error = "No se encontro la regla de automatizacion." });

        await _auditoria.RegistrarAsync("AUTOMATIZACION_ELIMINADA", "AUTOMATIZACION", id, "Regla eliminada.");
        return Ok(new { message = "Regla eliminada correctamente." });
    }

    [HttpPost("probar")]
    [Authorize(Policy = Permissions.AutomatizacionesCrear)]
    public async Task<IActionResult> Test(AutomatizacionTestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TipoEvento))
            return BadRequest(new { error = "Selecciona el evento que deseas probar." });

        var result = await _engine.EvaluarEventoAsync(
            request.TipoEvento,
            request.EntidadTipo,
            request.EntidadId,
            request.Datos,
            AutomationExecutionMode.Test);

        await _auditoria.RegistrarAsync("AUTOMATIZACION_PROBADA", request.EntidadTipo, request.EntidadId, $"Prueba ejecutada para {request.TipoEvento}.");
        return Ok(result);
    }

    private static string? ValidateRequest(AutomatizacionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return "El nombre de la regla es requerido.";

        if (string.IsNullOrWhiteSpace(request.TipoEvento))
            return "Selecciona el evento que dispara la regla.";

        if (request.Acciones.Count == 0)
            return "Agrega al menos una accion.";

        foreach (var condition in request.Condiciones)
        {
            if (string.IsNullOrWhiteSpace(condition.Campo))
                return "Todas las condiciones deben tener un campo.";

            if (string.IsNullOrWhiteSpace(condition.Operador))
                return "Todas las condiciones deben tener un operador.";
        }

        foreach (var action in request.Acciones)
        {
            if (string.IsNullOrWhiteSpace(action.TipoAccion))
                return "Todas las acciones deben tener un tipo.";

            if (!string.IsNullOrWhiteSpace(action.ParametrosJson))
            {
                try
                {
                    System.Text.Json.JsonDocument.Parse(action.ParametrosJson);
                }
                catch
                {
                    return "Los parametros de una accion no tienen formato JSON valido.";
                }
            }
        }

        return null;
    }
}
