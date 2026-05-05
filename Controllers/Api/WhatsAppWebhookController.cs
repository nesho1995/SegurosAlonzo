using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Services;
using System.Text.Json;

namespace ReclamosWhatsApp.Controllers.Api;

/// <summary>
/// Endpoint que Meta llama para:
///   GET  — verificar que el webhook existe (durante la configuración en Meta Business Manager)
///   POST — recibir notificaciones: estados de mensajes (enviado, entregado, leído, fallido)
///          e mensajes entrantes de clientes
/// </summary>
[ApiController]
[Route("api/webhook/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly AppSettingsRepository _settings;
    private readonly IConfiguration _config;
    private readonly AuditoriaService _auditoria;
    private readonly WhatsAppConversacionRepository _conversaciones;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        AppSettingsRepository settings,
        IConfiguration config,
        AuditoriaService auditoria,
        WhatsAppConversacionRepository conversaciones,
        ILogger<WhatsAppWebhookController> logger)
    {
        _settings = settings;
        _config = config;
        _auditoria = auditoria;
        _conversaciones = conversaciones;
        _logger = logger;
    }

    // ─── Verificación del webhook (Meta llama esto al configurarlo) ───────────
    // Meta envía: hub.mode=subscribe, hub.verify_token=TU_TOKEN, hub.challenge=RETO
    // Debes responder con el valor de hub.challenge para confirmar.

    [HttpGet]
    public async Task<IActionResult> Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (mode != "subscribe")
            return BadRequest("hub.mode debe ser 'subscribe'");

        var waConfig = await _settings.GetWhatsAppConfigAsync(_config, includeSecret: true);
        var expected = waConfig.WebhookVerifyToken;

        if (string.IsNullOrWhiteSpace(expected))
            return StatusCode(503, "Webhook verify token no configurado.");

        if (!string.Equals(verifyToken, expected, StringComparison.Ordinal))
        {
            _logger.LogWarning("WhatsApp webhook: token de verificación incorrecto");
            return Forbid();
        }

        _logger.LogInformation("WhatsApp webhook verificado correctamente por Meta");
        return Content(challenge ?? "", "text/plain");
    }

    // ─── Callbacks de Meta (estados de mensajes, mensajes entrantes) ──────────

    [HttpPost]
    public async Task<IActionResult> Receive()
    {
        string body;
        using (var reader = new StreamReader(Request.Body))
            body = await reader.ReadToEndAsync();

        _logger.LogDebug("WhatsApp webhook recibido: {Body}", body);

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("entry", out var entries))
                return Ok(); // Meta espera 200 aunque no procesemos

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes)) continue;

                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value)) continue;

                    // Actualizaciones de estado (enviado, entregado, leído, fallido)
                    if (value.TryGetProperty("statuses", out var statuses))
                        await ProcessStatuses(statuses);

                    // Mensajes entrantes de clientes
                    if (value.TryGetProperty("messages", out var messages))
                        await ProcessMessages(messages, value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando webhook de WhatsApp");
            // Siempre devolver 200 a Meta o reintentará indefinidamente
        }

        return Ok();
    }

    // ─── Procesamiento de estados ─────────────────────────────────────────────

    private async Task ProcessStatuses(JsonElement statuses)
    {
        foreach (var status in statuses.EnumerateArray())
        {
            var messageId  = status.TryGetProperty("id", out var id) ? id.GetString() : null;
            var estado     = status.TryGetProperty("status", out var st) ? st.GetString() : null;
            var recipient  = status.TryGetProperty("recipient_id", out var r) ? r.GetString() : null;
            var timestamp  = status.TryGetProperty("timestamp", out var ts) ? ts.GetString() : null;

            _logger.LogInformation(
                "WhatsApp status: {MessageId} → {Estado} para {Recipient} en {Timestamp}",
                messageId, estado, recipient, timestamp);

            // Actualizar estado en la tabla de mensajes
            if (!string.IsNullOrWhiteSpace(messageId) && !string.IsNullOrWhiteSpace(estado))
            {
                var estadoNorm = estado.ToLowerInvariant() switch
                {
                    "sent"      => "enviado",
                    "delivered" => "entregado",
                    "read"      => "leido",
                    "failed"    => "error",
                    _           => estado
                };
                try { await _conversaciones.ActualizarEstadoMensajeAsync(messageId, estadoNorm); }
                catch (Exception ex) { _logger.LogWarning(ex, "No se pudo actualizar estado de mensaje {Id}", messageId); }
            }

            // Estado 'failed' → registrar en auditoría para visibilidad
            if (string.Equals(estado, "failed", StringComparison.OrdinalIgnoreCase))
            {
                var errorMsg = "";
                if (status.TryGetProperty("errors", out var errors))
                {
                    foreach (var err in errors.EnumerateArray())
                    {
                        var code = err.TryGetProperty("code", out var c) ? c.GetInt32().ToString() : "";
                        var title = err.TryGetProperty("title", out var t) ? t.GetString() : "";
                        errorMsg += $"[{code}] {title} ";
                    }
                }

                await _auditoria.RegistrarAsync(
                    "WHATSAPP_MENSAJE_FALLIDO",
                    "WHATSAPP",
                    null,
                    $"Mensaje {messageId} falló para {recipient}: {errorMsg.Trim()}");
            }
        }
    }

    // ─── Procesamiento de mensajes entrantes ──────────────────────────────────

    private async Task ProcessMessages(JsonElement messages, JsonElement value)
    {
        foreach (var msg in messages.EnumerateArray())
        {
            var from      = msg.TryGetProperty("from", out var f) ? f.GetString() : null;
            var msgType   = msg.TryGetProperty("type", out var t) ? t.GetString() : null;
            var messageId = msg.TryGetProperty("id", out var id) ? id.GetString() : null;

            string? texto = null;
            if (msgType == "text" && msg.TryGetProperty("text", out var textObj))
                texto = textObj.TryGetProperty("body", out var b) ? b.GetString() : null;

            // Extraer media si aplica
            string? mediaId = null;
            string? mediaMime = null;
            string? mediaNombre = null;
            if (msgType is "image" or "document" or "audio" or "video" or "sticker")
            {
                if (msg.TryGetProperty(msgType, out var mediaObj))
                {
                    mediaId    = mediaObj.TryGetProperty("id", out var mid) ? mid.GetString() : null;
                    mediaMime  = mediaObj.TryGetProperty("mime_type", out var mime) ? mime.GetString() : null;
                    mediaNombre = mediaObj.TryGetProperty("filename", out var fn) ? fn.GetString() : null;
                    if (string.IsNullOrWhiteSpace(texto))
                        texto = mediaObj.TryGetProperty("caption", out var cap) ? cap.GetString() : null;
                }
            }

            // Nombre del contacto si viene en el payload
            string? nombre = null;
            if (value.TryGetProperty("contacts", out var contacts))
            {
                foreach (var contact in contacts.EnumerateArray())
                {
                    if (contact.TryGetProperty("wa_id", out var waId) && waId.GetString() == from)
                    {
                        if (contact.TryGetProperty("profile", out var profile))
                            nombre = profile.TryGetProperty("name", out var n) ? n.GetString() : null;
                        break;
                    }
                }
            }

            _logger.LogInformation(
                "WhatsApp mensaje entrante: {MessageId} de {From} ({Nombre}): {Tipo} — {Texto}",
                messageId, from, nombre ?? "desconocido", msgType, texto ?? "(no texto)");

            // Guardar en bandeja de conversaciones
            if (!string.IsNullOrWhiteSpace(from))
            {
                try
                {
                    var convId = await _conversaciones.GetOrCreateConversacionAsync(from, nombre);
                    await _conversaciones.SaveMensajeAsync(new WhatsAppMensaje
                    {
                        ConversacionId    = convId,
                        WhatsappMessageId = messageId,
                        Direccion         = "entrante",
                        TipoContenido     = msgType ?? "texto",
                        Contenido         = texto,
                        MediaId           = mediaId,
                        MediaTipoMime     = mediaMime,
                        MediaNombre       = mediaNombre,
                        Estado            = "recibido"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error guardando mensaje entrante en bandeja para {From}", from);
                }
            }

            await _auditoria.RegistrarAsync(
                "WHATSAPP_MENSAJE_RECIBIDO",
                "WHATSAPP",
                null,
                $"Mensaje de {nombre ?? from} ({from}): {texto ?? $"[{msgType}]"}");
        }
    }
}
