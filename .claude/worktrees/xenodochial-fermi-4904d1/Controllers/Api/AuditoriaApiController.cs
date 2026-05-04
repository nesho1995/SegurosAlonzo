using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize(Policy = Permissions.AuditoriaVer)]
[Route("api/auditoria")]
public class AuditoriaApiController : ControllerBase
{
    private readonly AuditoriaRepository _auditoria;

    public AuditoriaApiController(AuditoriaRepository auditoria)
    {
        _auditoria = auditoria;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] AuditoriaFiltro filtro)
    {
        var (items, total) = await _auditoria.GetAsync(filtro);
        return Ok(new
        {
            items,
            total,
            pagina = filtro.Pagina,
            pageSize = filtro.PageSize,
            totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)filtro.PageSize))
        });
    }
}
