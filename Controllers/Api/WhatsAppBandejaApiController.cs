using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;
using System.Security.Claims;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Route("api/whatsapp/bandeja")]
[Authorize(Policy = Permissions.WhatsAppBandejaVer)]
public class WhatsAppBandejaApiController : ControllerBase
{
    private readonly WhatsAppConversacionRepository _repo;
    private readonly WhatsAppService _whatsapp;
    private readonly AuditoriaService _auditoria;

    public WhatsAppBandejaApiController(
        WhatsAppConversacionRepository repo,
        WhatsAppService whatsapp,
        AuditoriaService auditoria)
    {
        _repo = repo;
        _whatsapp = whatsapp;
        _auditoria = auditoria;
    }

    // GET /api/whatsapp/bandeja
    [HttpGet]
    public async Task<IActionResult> GetConversaciones(
        [FromQuery] string? estado,
        [FromQuery] string? buscar,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        if (limit is < 1 or > 200) limit = 50;
        var (items, total) = await _repo.GetConversacionesAsync(estado, buscar, limit, offset);
        var totalNoLeidos = await _repo.GetTotalNoLeidosAsync();
        return Ok(new { items, total, totalNoLeidos });
    }

    // GET /api/whatsapp/bandeja/{id}/mensajes
    [HttpGet("{id:int}/mensajes")]
    public async Task<IActionResult> GetMensajes(
        int id,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var conv = await _repo.GetConversacionByIdAsync(id);
        if (conv is null) return NotFound(new { error = "Conversación no encontrada." });

        if (limit is < 1 or > 200) limit = 50;
        var (items, total) = await _repo.GetMensajesAsync(id, limit, offset);
        return Ok(new { conversacion = conv, items, total });
    }

    // POST /api/whatsapp/bandeja/{id}/responder
    [HttpPost("{id:int}/responder")]
    [Authorize(Policy = Permissions.WhatsAppBandejaResponder)]
    public async Task<IActionResult> Responder(int id, [FromBody] RespuestaRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Mensaje))
            return BadRequest(new { error = "El mensaje no puede estar vacío." });

        var conv = await _repo.GetConversacionByIdAsync(id);
        if (conv is null) return NotFound(new { error = "Conversación no encontrada." });

        var usuarioId = GetUserId();
        var (ok, response) = await _whatsapp.SendTextAsync(conv.Telefono, req.Mensaje, usuarioId);

        if (!ok)
            return BadRequest(new { error = response });

        await _auditoria.RegistrarAsync(
            "WHATSAPP_RESPUESTA_BANDEJA", "WHATSAPP", null,
            $"Respuesta enviada a {conv.NombreContacto ?? conv.Telefono}: {req.Mensaje[..Math.Min(100, req.Mensaje.Length)]}");

        return Ok(new { ok = true });
    }

    // POST /api/whatsapp/bandeja/{id}/marcar-leido
    [HttpPost("{id:int}/marcar-leido")]
    public async Task<IActionResult> MarcarLeido(int id)
    {
        var conv = await _repo.GetConversacionByIdAsync(id);
        if (conv is null) return NotFound(new { error = "Conversación no encontrada." });
        await _repo.MarcarLeidoAsync(id);
        return Ok(new { ok = true });
    }

    // PUT /api/whatsapp/bandeja/{id}/estado
    [HttpPut("{id:int}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] EstadoRequest req)
    {
        var estadosValidos = new[] { "abierta", "en_espera", "resuelta" };
        if (!estadosValidos.Contains(req.Estado))
            return BadRequest(new { error = "Estado inválido. Use: abierta, en_espera, resuelta." });

        var conv = await _repo.GetConversacionByIdAsync(id);
        if (conv is null) return NotFound(new { error = "Conversación no encontrada." });

        await _repo.CambiarEstadoAsync(id, req.Estado);
        return Ok(new { ok = true });
    }

    // POST /api/whatsapp/bandeja/{id}/asociar-cliente
    [HttpPost("{id:int}/asociar-cliente")]
    public async Task<IActionResult> AsociarCliente(int id, [FromBody] AsociarClienteRequest req)
    {
        var conv = await _repo.GetConversacionByIdAsync(id);
        if (conv is null) return NotFound(new { error = "Conversación no encontrada." });
        await _repo.AsociarClienteAsync(id, req.ClienteId);
        return Ok(new { ok = true });
    }

    // POST /api/whatsapp/bandeja/{id}/asociar-reclamo
    [HttpPost("{id:int}/asociar-reclamo")]
    public async Task<IActionResult> AsociarReclamo(int id, [FromBody] AsociarReclamoRequest req)
    {
        var conv = await _repo.GetConversacionByIdAsync(id);
        if (conv is null) return NotFound(new { error = "Conversación no encontrada." });
        await _repo.AsociarReclamoAsync(id, req.ReclamoId);
        var convActualizada = await _repo.GetConversacionByIdAsync(id);
        return Ok(new { ok = true, conversacion = convActualizada });
    }

    // POST /api/whatsapp/bandeja/{id}/asignar-agente
    [HttpPost("{id:int}/asignar-agente")]
    public async Task<IActionResult> AsignarAgente(int id, [FromBody] AsignarAgenteRequest req)
    {
        var conv = await _repo.GetConversacionByIdAsync(id);
        if (conv is null) return NotFound(new { error = "Conversación no encontrada." });
        await _repo.AsignarAgenteAsync(id, req.AgenteId);
        return Ok(new { ok = true });
    }

    // GET /api/whatsapp/bandeja/agentes
    [HttpGet("agentes")]
    public async Task<IActionResult> GetAgentes()
    {
        var agentes = await _repo.GetAgentesAsync();
        return Ok(agentes);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }
}

public record RespuestaRequest(string Mensaje);
public record EstadoRequest(string Estado);
public record AsociarClienteRequest(int ClienteId);
public record AsociarReclamoRequest(int? ReclamoId);
public record AsignarAgenteRequest(int? AgenteId);
