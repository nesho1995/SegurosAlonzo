using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/catalogos")]
public class CatalogosApiController : ControllerBase
{
    private readonly CatalogoRepository _catalogos;
    private readonly AuditoriaService _auditoria;

    public CatalogosApiController(CatalogoRepository catalogos, AuditoriaService auditoria)
    {
        _catalogos = catalogos;
        _auditoria = auditoria;
    }

    [HttpGet]
    public async Task<IActionResult> GetTipos()
    {
        return Ok(new { tipos = await _catalogos.GetTiposAsync() });
    }

    [HttpGet("{tipo}")]
    public async Task<IActionResult> GetByTipo(string tipo, bool incluirInactivos = true)
    {
        return Ok(new { items = await _catalogos.GetByTipoAsync(tipo.Trim().ToUpperInvariant(), incluirInactivos) });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.ConfiguracionAdministrar)]
    public async Task<IActionResult> Upsert([FromBody] CatalogoItem item)
    {
        if (string.IsNullOrWhiteSpace(item.TipoCatalogo) || string.IsNullOrWhiteSpace(item.Nombre))
            return BadRequest(new { error = "Tipo y nombre son obligatorios." });
        item.TipoCatalogo = item.TipoCatalogo.Trim().ToUpperInvariant();
        item.Codigo = string.IsNullOrWhiteSpace(item.Codigo) ? item.Nombre : item.Codigo;
        var id = await _catalogos.UpsertAsync(item);
        await _auditoria.RegistrarAsync("CATALOGO_GUARDADO", "CATALOGO", id, $"Catalogo {item.TipoCatalogo}: {item.Nombre}");
        return Ok(new { id });
    }

    [HttpPatch("{id:int}/activo")]
    [Authorize(Policy = Permissions.ConfiguracionAdministrar)]
    public async Task<IActionResult> SetActivo(int id, [FromBody] CatalogoActivoRequest request)
    {
        await _catalogos.SetActivoAsync(id, request.Activo);
        await _auditoria.RegistrarAsync(request.Activo ? "CATALOGO_ACTIVADO" : "CATALOGO_INACTIVADO", "CATALOGO", id, "Estado de catalogo actualizado.");
        return NoContent();
    }

    [HttpPost("merge")]
    [Authorize(Policy = Permissions.ConfiguracionAdministrar)]
    public async Task<IActionResult> Merge([FromBody] CatalogoMergeRequest request)
    {
        await _catalogos.MergeAsync(request.SourceId, request.TargetId);
        await _auditoria.RegistrarAsync("CATALOGO_FUSIONADO", "CATALOGO", request.TargetId, $"Fusion de catalogo {request.SourceId} -> {request.TargetId}");
        return NoContent();
    }
}

public sealed record CatalogoActivoRequest(bool Activo);
