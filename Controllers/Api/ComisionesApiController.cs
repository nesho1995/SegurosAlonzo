using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize(Policy = Permissions.ComisionesVer)]
[Route("api/comisiones")]
public class ComisionesApiController : ControllerBase
{
    private readonly ComisionRepository _repo;
    private readonly ComisionImportService _import;
    private readonly AuditoriaService _auditoria;

    public ComisionesApiController(ComisionRepository repo, ComisionImportService import, AuditoriaService auditoria)
    {
        _repo = repo;
        _import = import;
        _auditoria = auditoria;
    }

    [HttpGet]
    public async Task<IActionResult> Get(int? loteId = null)
    {
        var (lotes, detalles) = await _repo.GetAsync(loteId);
        return Ok(new { lotes, detalles });
    }

    [HttpPost("preview")]
    [Authorize(Policy = Permissions.ComisionesCargar)]
    public async Task<IActionResult> Preview([FromForm] IFormFile archivo)
    {
        var error = ValidateExcel(archivo);
        if (error is not null)
            return BadRequest(new { error });

        using var stream = archivo.OpenReadStream();
        return Ok(new { items = await _import.PreviewAsync(stream) });
    }

    [HttpPost("importar")]
    [Authorize(Policy = Permissions.ComisionesCargar)]
    public async Task<IActionResult> Importar([FromForm] IFormFile archivo)
    {
        var error = ValidateExcel(archivo);
        if (error is not null)
            return BadRequest(new { error });

        using var stream = archivo.OpenReadStream();
        var loteId = await _import.ImportAsync(stream, archivo.FileName, CurrentUserId());
        await _auditoria.RegistrarAsync("CARGAR_COMISIONES", "COMISIONES", loteId, $"Reporte de comisiones cargado: {archivo.FileName}");
        return Ok(new { loteId });
    }

    [HttpPatch("detalle/{id:int}/revisado")]
    [Authorize(Policy = Permissions.ComisionesEditar)]
    public async Task<IActionResult> Revisado(int id)
    {
        await _repo.MarcarRevisadoAsync(id, CurrentUserId());
        await _auditoria.RegistrarAsync("REVISAR_COMISION", "COMISIONES", id, "Diferencia de comision marcada como revisada.");
        return NoContent();
    }

    private static string? ValidateExcel(IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0) return "Selecciona un archivo.";
        if (Path.GetExtension(archivo.FileName).ToLowerInvariant() != ".xlsx") return "Solo se permiten archivos Excel.";
        if (archivo.Length > 5 * 1024 * 1024) return "El archivo no debe superar 5 MB.";
        return null;
    }

    private int? CurrentUserId()
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }
}
