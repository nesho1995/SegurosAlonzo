using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/notificaciones")]
public class NotificacionesApiController : ControllerBase
{
    private readonly NotificacionRepository _notificaciones;

    public NotificacionesApiController(NotificacionRepository notificaciones)
    {
        _notificaciones = notificaciones;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var (items, unread) = await _notificaciones.GetAsync(GetUsuarioId());
        return Ok(new { items, unread });
    }

    [HttpPost("{id:int}/leida")]
    public async Task<IActionResult> MarcarLeida(int id)
    {
        await _notificaciones.MarcarLeidaAsync(id, GetUsuarioId());
        return Ok(new { message = "Notificacion marcada como leida." });
    }

    private int? GetUsuarioId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }
}
