using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize(Policy = Permissions.GastosVer)]
[Route("api/gastos")]
public class GastosApiController : ControllerBase
{
    private readonly GastoRepository _gastos;
    private readonly AuditoriaService _auditoria;

    public GastosApiController(GastoRepository gastos, AuditoriaService auditoria)
    {
        _gastos = gastos;
        _auditoria = auditoria;
    }

    [HttpGet]
    public async Task<IActionResult> Get(DateTime? desde = null, DateTime? hasta = null, string? categoria = null, string? estado = null, int pagina = 1, int pageSize = 25)
    {
        var (items, total, totalRango) = await _gastos.GetAsync(desde, hasta, categoria, estado, pagina, pageSize);
        var resumen = await _gastos.GetResumenAsync();
        return Ok(new { items, total, pagina, pageSize, totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)), totalRango, resumen });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.GastosCrear)]
    public async Task<IActionResult> Create(Gasto gasto)
    {
        gasto.Estado = "REGISTRADO";
        var error = Validate(gasto);
        if (error is not null)
            return BadRequest(new { error });

        var id = await _gastos.CreateAsync(gasto, CurrentUserId());
        await _auditoria.RegistrarAsync("CREAR_GASTO", "GASTO", id, $"Gasto registrado: {gasto.Descripcion}");
        return Ok(new { id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.GastosEditar)]
    public async Task<IActionResult> Update(int id, Gasto gasto)
    {
        gasto.Estado = "REGISTRADO";
        if (id != gasto.Id)
            return BadRequest(new { error = "El gasto seleccionado no coincide." });
        if (await _gastos.GetByIdAsync(id) is null)
            return NotFound(new { error = "El gasto no existe." });

        var error = Validate(gasto);
        if (error is not null)
            return BadRequest(new { error });

        await _gastos.UpdateAsync(gasto);
        await _auditoria.RegistrarAsync("EDITAR_GASTO", "GASTO", id, $"Gasto actualizado: {gasto.Descripcion}");
        return NoContent();
    }

    [HttpPatch("{id:int}/estado")]
    [Authorize(Policy = Permissions.GastosEliminar)]
    public async Task<IActionResult> SetActivo(int id, [FromBody] GastoEstadoRequest request)
    {
        if (await _gastos.GetByIdAsync(id) is null)
            return NotFound(new { error = "El gasto no existe." });

        await _gastos.SetActivoAsync(id, request.Activo);
        await _auditoria.RegistrarAsync(request.Activo ? "REACTIVAR_GASTO" : "DESACTIVAR_GASTO", "GASTO", id, request.Activo ? "Gasto reactivado." : "Gasto desactivado.");
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.GastosEliminar)]
    public async Task<IActionResult> Delete(int id)
    {
        if (await _gastos.GetByIdAsync(id) is null)
            return NotFound(new { error = "El gasto no existe." });

        await _gastos.DeleteAsync(id);
        await _auditoria.RegistrarAsync("ELIMINAR_GASTO", "GASTO", id, "Gasto eliminado.");
        return NoContent();
    }

    private static string? Validate(Gasto gasto)
    {
        if (string.IsNullOrWhiteSpace(gasto.Descripcion)) return "La descripcion es obligatoria.";
        if (string.IsNullOrWhiteSpace(gasto.Categoria)) return "Selecciona una categoria.";
        if (gasto.Monto <= 0) return "El monto debe ser mayor que cero.";
        if (gasto.Fecha == default) return "Selecciona una fecha.";
        return null;
    }

    private int? CurrentUserId()
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }
}

public sealed class GastoEstadoRequest
{
    public bool Activo { get; set; }
}
