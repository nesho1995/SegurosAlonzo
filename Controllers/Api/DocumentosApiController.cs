using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/documentos")]
public class DocumentosApiController : ControllerBase
{
    private readonly DocumentoRepository _documentos;
    private readonly DocumentoStorageService _storage;
    private readonly ReclamoRepository _reclamos;
    private readonly AuditoriaService _auditoria;

    public DocumentosApiController(DocumentoRepository documentos, DocumentoStorageService storage, ReclamoRepository reclamos, AuditoriaService auditoria)
    {
        _documentos = documentos;
        _storage = storage;
        _reclamos = reclamos;
        _auditoria = auditoria;
    }

    [HttpPost("upload")]
    [Authorize(Policy = Permissions.DocumentosSubir)]
    [EnableRateLimiting("upload")]
    public async Task<IActionResult> Upload([FromForm] IFormFile archivo, [FromForm] string entidadTipo, [FromForm] int entidadId, [FromForm] string? tipoDocumento, [FromForm] string? observacion)
    {
        try
        {
            var usuarioId = GetUsuarioId();
            var documento = await _storage.GuardarAsync(archivo, entidadTipo, entidadId, tipoDocumento ?? "General", usuarioId);
            if (!string.IsNullOrWhiteSpace(observacion))
            {
                await _documentos.UpdateObservacionAsync(documento.Id, observacion);
                documento.Observacion = observacion.Trim();
            }
            await _auditoria.RegistrarAsync("SUBIR_DOCUMENTO", entidadTipo.Trim().ToUpperInvariant(), entidadId, $"Documento subido: {documento.NombreArchivoOriginal}.");
            return Ok(documento);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch
        {
            return BadRequest(new { error = "No se pudo guardar el documento. Intenta nuevamente." });
        }
    }

    [HttpGet("{entidadTipo}/{entidadId:int}")]
    [Authorize(Policy = Permissions.DocumentosVer)]
    public async Task<IActionResult> Get(string entidadTipo, int entidadId)
    {
        var items = await _documentos.GetByEntidadAsync(entidadTipo.Trim().ToUpperInvariant(), entidadId);
        return Ok(new { items });
    }

    [HttpGet("download/{id:int}")]
    [Authorize(Policy = Permissions.DocumentosVer)]
    public async Task<IActionResult> Download(int id)
    {
        try
        {
            var (documento, absolutePath) = await _storage.PrepararDescargaAsync(id);
            var contentType = string.IsNullOrWhiteSpace(documento.MimeType) ? GetContentType(documento.Extension) : documento.MimeType;
            await _auditoria.RegistrarAsync("DESCARGAR_DOCUMENTO", documento.EntidadTipo, documento.EntidadId, $"Documento descargado: {documento.NombreArchivoOriginal}.");
            return PhysicalFile(absolutePath, contentType, documento.NombreArchivoOriginal);
        }
        catch
        {
            return NotFound(new { error = "El documento no esta disponible." });
        }
    }

    [HttpGet("{id:int}/ver")]
    [Authorize(Policy = Permissions.DocumentosVer)]
    public async Task<IActionResult> Ver(int id)
    {
        try
        {
            var (documento, absolutePath) = await _storage.PrepararDescargaAsync(id);
            var contentType = string.IsNullOrWhiteSpace(documento.MimeType) ? GetContentType(documento.Extension) : documento.MimeType;
            Response.Headers.ContentDisposition = BuildInlineContentDisposition(documento.NombreArchivoOriginal);
            await _auditoria.RegistrarAsync("VER_DOCUMENTO", documento.EntidadTipo, documento.EntidadId, $"Documento visualizado: {documento.NombreArchivoOriginal}.");
            return PhysicalFile(absolutePath, contentType);
        }
        catch
        {
            return NotFound(new { error = "El documento no esta disponible." });
        }
    }

    [HttpGet("{id:int}/descargar")]
    [Authorize(Policy = Permissions.DocumentosVer)]
    public async Task<IActionResult> Descargar(int id)
    {
        try
        {
            var (documento, absolutePath) = await _storage.PrepararDescargaAsync(id);
            var contentType = string.IsNullOrWhiteSpace(documento.MimeType) ? GetContentType(documento.Extension) : documento.MimeType;
            await _auditoria.RegistrarAsync("DESCARGAR_DOCUMENTO", documento.EntidadTipo, documento.EntidadId, $"Documento descargado: {documento.NombreArchivoOriginal}.");
            return PhysicalFile(absolutePath, contentType, documento.NombreArchivoOriginal);
        }
        catch
        {
            return NotFound(new { error = "El documento no esta disponible." });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.DocumentosEliminar)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var documento = await _documentos.GetByIdAsync(id);
            await _storage.EliminarAsync(id);
            if (documento is not null && string.Equals(documento.EntidadTipo, "RECLAMO", StringComparison.OrdinalIgnoreCase))
            {
                await _reclamos.RecalcularDocumentoPorTipoAsync(documento.EntidadId, documento.TipoDocumento);
                var completo = await _reclamos.TodosDocumentosRecibidosAsync(documento.EntidadId);
                await _reclamos.UpdateEstadoAsync(documento.EntidadId, completo ? "COMPLETO" : "EN_SEGUIMIENTO");
            }
            await _auditoria.RegistrarAsync("ELIMINAR_DOCUMENTO", "DOCUMENTO", id, "Documento eliminado.");
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { error = "El documento no se puede eliminar en este momento." });
        }
        catch
        {
            return BadRequest(new { error = "No se pudo eliminar el documento." });
        }
    }

    private int? GetUsuarioId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }

    private static string GetContentType(string extension)
    {
        return extension.TrimStart('.').ToLowerInvariant() switch
        {
            "pdf" => "application/pdf",
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            _ => "application/octet-stream"
        };
    }

    [HttpPut("{id:int}/observacion")]
    [Authorize(Policy = Permissions.DocumentosSubir)]
    public async Task<IActionResult> UpdateObservacion(int id, [FromBody] DocumentoObservacionRequest request)
    {
        await _documentos.UpdateObservacionAsync(id, request.Observacion);
        await _auditoria.RegistrarAsync("ACTUALIZAR_OBSERVACION_DOCUMENTO", "DOCUMENTO", id, "Observacion de documento actualizada.");
        return NoContent();
    }

    private static string BuildInlineContentDisposition(string? fileName)
    {
        var safeName = SanitizeHeaderFileName(fileName);
        var asciiFallback = new string(safeName.Select(ch => ch <= 127 ? ch : '_').ToArray());
        var encoded = Uri.EscapeDataString(safeName);
        return $"inline; filename=\"{asciiFallback}\"; filename*=UTF-8''{encoded}";
    }

    private static string SanitizeHeaderFileName(string? fileName)
    {
        var cleaned = new string((fileName ?? "documento")
            .Where(ch => !char.IsControl(ch) && ch != '"' && ch != '\\' && ch != '/')
            .ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(cleaned))
            return "documento";

        return cleaned.Length > 180 ? cleaned[..180] : cleaned;
    }
}

public sealed record DocumentoObservacionRequest(string? Observacion);
