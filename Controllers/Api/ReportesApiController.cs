using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize(Policy = Permissions.ReclamosVer)]
[Route("api/reportes")]
public class ReportesApiController : ControllerBase
{
    private readonly ReporteReclamosRepository _reportes;

    public ReportesApiController(ReporteReclamosRepository reportes)
    {
        _reportes = reportes;
    }

    [HttpGet("reclamos")]
    public async Task<IActionResult> Reclamos([FromQuery] ReporteReclamosFiltro filtro)
    {
        if (!filtro.Desde.HasValue && !filtro.Hasta.HasValue)
            filtro.Desde = DateTime.Today.AddDays(-7);

        var (items, total) = await _reportes.GetReclamosAsync(filtro);
        var list = items.ToList();
        var resumen = new ReporteReclamosResumen
        {
            Total = total,
            ConPendientes = list.Count(x => x.DocumentosPendientes > 0),
            SinMovimientoPeriodo = list.Count(x => x.EventosPeriodo == 0),
            ConTelefono = list.Count(x => !string.IsNullOrWhiteSpace(x.Celular)),
            SinTelefono = list.Count(x => string.IsNullOrWhiteSpace(x.Celular))
        };

        return Ok(new { items = list, total, resumen });
    }
}
