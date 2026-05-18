using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;
using ReclamosWhatsApp.Services;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Route("api/whatsapp/bandeja")]
[Authorize(Policy = Permissions.WhatsAppBandejaVer)]
public class WhatsAppBandejaApiController : ControllerBase
{
    private readonly WhatsAppConversacionRepository _repo;
    private readonly ReclamoRepository _reclamos;
    private readonly DocumentoRepository _documentos;
    private readonly WhatsAppService _whatsapp;
    private readonly DocumentoStorageService _documentoStorage;
    private readonly EmailSenderService _emailSender;
    private readonly AppSettingsRepository _settings;
    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly AuditoriaService _auditoria;
    private readonly ILogger<WhatsAppBandejaApiController> _logger;

    public WhatsAppBandejaApiController(
        WhatsAppConversacionRepository repo,
        ReclamoRepository reclamos,
        DocumentoRepository documentos,
        WhatsAppService whatsapp,
        DocumentoStorageService documentoStorage,
        EmailSenderService emailSender,
        AppSettingsRepository settings,
        IConfiguration config,
        HttpClient http,
        AuditoriaService auditoria,
        ILogger<WhatsAppBandejaApiController> logger)
    {
        _repo = repo;
        _reclamos = reclamos;
        _documentos = documentos;
        _whatsapp = whatsapp;
        _documentoStorage = documentoStorage;
        _emailSender = emailSender;
        _settings = settings;
        _config = config;
        _http = http;
        _auditoria = auditoria;
        _logger = logger;
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

    // GET /api/whatsapp/bandeja/reclamos
    [HttpGet("reclamos")]
    public async Task<IActionResult> BuscarReclamos([FromQuery] string? buscar, [FromQuery] string? telefono)
    {
        var text = buscar?.Trim() ?? "";
        var tel = OnlyDigits(telefono);
        var reclamos = await _reclamos.GetAllAsync();

        var filtrados = reclamos
            .Where(x =>
                string.IsNullOrWhiteSpace(text)
                || (x.Reclamo ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)
                || (x.NumeroReclamo ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)
                || (x.Conductor ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)
                || (x.Asegurado ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)
                || (x.Poliza ?? "").Contains(text, StringComparison.OrdinalIgnoreCase)
                || (x.Placa ?? "").Contains(text, StringComparison.OrdinalIgnoreCase))
            .Select(x => new ReclamoLinkOption(
                x.Id,
                x.NumeroReclamo ?? x.Reclamo ?? $"#{x.Id}",
                x.Conductor ?? x.Asegurado ?? "Cliente",
                x.Poliza,
                x.Placa,
                x.Celular,
                x.FechaNotificacion,
                x.EstadoReclamo ?? x.Estado,
                PhoneMatches(OnlyDigits(x.Celular), tel)))
            .OrderByDescending(x => x.TelefonoCoincide)
            .ThenByDescending(x => x.FechaNotificacion ?? DateTime.MinValue)
            .Take(25)
            .ToList();

        return Ok(filtrados);
    }

    // POST /api/whatsapp/bandeja/mensajes/{mensajeId}/guardar-documento
    [HttpPost("mensajes/{mensajeId:int}/guardar-documento")]
    [Authorize(Policy = Permissions.DocumentosSubir)]
    public async Task<IActionResult> GuardarDocumentoDesdeMensaje(int mensajeId, [FromBody] GuardarDocumentoWhatsAppRequest req)
    {
        if (req.ReclamoId <= 0)
            return BadRequest(new { error = "Selecciona un reclamo valido." });

        var reclamo = await _reclamos.GetByIdAsync(req.ReclamoId);
        if (reclamo is null)
            return NotFound(new { error = "Reclamo no encontrado." });

        var msg = await _repo.GetMensajeByIdAsync(mensajeId);
        if (msg is null || string.IsNullOrWhiteSpace(msg.MediaId))
            return NotFound(new { error = "El mensaje no tiene archivo para guardar." });
        if (!string.Equals(msg.Direccion, "entrante", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Solo se guardan archivos recibidos del cliente." });

        await _reclamos.CrearDocumentosInicialesAsync(req.ReclamoId);
        var documentosChecklist = (await _reclamos.GetDocumentosAsync(req.ReclamoId)).ToList();
        var seleccionado = documentosChecklist.FirstOrDefault(x => x.Id == req.ReclamoDocumentoId);
        if (seleccionado is null)
            return BadRequest(new { error = "Selecciona el check de documento que corresponde." });

        try
        {
            var media = await DescargarMediaMetaAsync(msg);
            await using (media.Stream)
            {
                var documento = await _documentoStorage.GuardarDesdeStreamAsync(
                    media.Stream,
                    media.FileName,
                    media.ContentType,
                    media.ContentLength,
                    "RECLAMO",
                    req.ReclamoId,
                    seleccionado.Documento,
                    GetUserId(),
                    req.Observacion);

                var yaEstabaCompleto = await _reclamos.TodosDocumentosRecibidosAsync(req.ReclamoId);
                await _reclamos.ActualizarDocumentoSegunAdjuntosAsync(seleccionado.Id, req.ReclamoId);
                var completo = await _reclamos.TodosDocumentosRecibidosAsync(req.ReclamoId);
                await _reclamos.UpdateEstadoAsync(req.ReclamoId, completo ? "COMPLETO" : "EN_SEGUIMIENTO");

                if (completo && !yaEstabaCompleto)
                {
                    var result = await _whatsapp.EnviarDocumentosRecibidosAsync(reclamo);
                    await _auditoria.RegistrarAsync(
                        result.ok ? "AVISO_DOCUMENTOS_RECIBIDOS" : "ERROR_AVISO_DOCUMENTOS_RECIBIDOS",
                        "RECLAMO",
                        req.ReclamoId,
                        result.ok ? "Cliente notificado por documentos completos." : result.response);

                    if (IsPostApprovalStage(reclamo) && !string.IsNullOrWhiteSpace(reclamo.CorreoAseguradoraPrincipal))
                    {
                        var documentos = (await _documentos.GetByEntidadAsync("RECLAMO", req.ReclamoId)).ToList();
                        var comprobantesFinales = documentos.Where(IsComprobanteFinal).ToList();
                        var correo = await _emailSender.EnviarDocumentosReclamoAsync(
                            reclamo,
                            reclamo.CorreoAseguradoraPrincipal,
                            comprobantesFinales,
                            reclamo.CorreoAseguradoraCopia,
                            soloComprobantesFinales: true);

                        await _auditoria.RegistrarAsync(
                            correo.ok ? "ENVIAR_COMPROBANTES_FINALES_ASEGURADORA_AUTO" : "ERROR_COMPROBANTES_FINALES_ASEGURADORA_AUTO",
                            "RECLAMO",
                            req.ReclamoId,
                            correo.ok ? $"Comprobantes finales enviados a {reclamo.CorreoAseguradoraPrincipal}." : correo.response);
                    }
                }

                await _auditoria.RegistrarAsync(
                    "WHATSAPP_DOCUMENTO_A_RECLAMO",
                    "RECLAMO",
                    req.ReclamoId,
                    $"Adjunto de WhatsApp guardado como {seleccionado.Documento}: {documento.NombreArchivoOriginal}.");

                var docs = await _reclamos.GetDocumentosAsync(req.ReclamoId);
                return Ok(new { documento, checklist = docs, completo });
            }
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error guardando media de WhatsApp {MensajeId} en reclamo {ReclamoId}", mensajeId, req.ReclamoId);
            return StatusCode(502, new { error = "No se pudo guardar el archivo recibido por WhatsApp." });
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }

    private static string OnlyDigits(string? value)
        => new((value ?? "").Where(char.IsDigit).ToArray());

    private static bool PhoneMatches(string claimPhone, string chatPhone)
    {
        if (string.IsNullOrWhiteSpace(claimPhone) || string.IsNullOrWhiteSpace(chatPhone))
            return false;
        if (claimPhone == chatPhone)
            return true;
        if (claimPhone.Length >= 8 && chatPhone.Length >= 8)
            return claimPhone[^8..] == chatPhone[^8..];
        return false;
    }

    private static bool IsPostApprovalStage(ReclamoWhatsApp reclamo)
    {
        return reclamo.AseguradoraAprobado
            || string.Equals(reclamo.Estado, "ASEGURADORA_APROBADO", StringComparison.OrdinalIgnoreCase)
            || string.Equals(reclamo.EstadoReclamo, "ASEGURADORA_APROBADO", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsComprobanteFinal(DocumentoDto documento)
    {
        var tipo = InsuranceResponseAnalyzer.NormalizeForMatch(documento.TipoDocumento);
        var nombre = InsuranceResponseAnalyzer.NormalizeForMatch(documento.NombreArchivoOriginal);
        var observacion = InsuranceResponseAnalyzer.NormalizeForMatch(documento.Observacion);
        var combined = $"{tipo} {nombre} {observacion}";
        return combined.Contains("RSA", StringComparison.Ordinal)
            || combined.Contains("RESTITUCION", StringComparison.Ordinal)
            || combined.Contains("RESTITUIR", StringComparison.Ordinal)
            || combined.Contains("COASEGURO", StringComparison.Ordinal)
            || combined.Contains("CO ASEGURO", StringComparison.Ordinal)
            || combined.Contains("CO-SEGURO", StringComparison.Ordinal)
            || combined.Contains("COPAGO", StringComparison.Ordinal)
            || combined.Contains("CO PAGO", StringComparison.Ordinal)
            || combined.Contains("CO-PAGO", StringComparison.Ordinal)
            || combined.Contains("COPARTICIPACION", StringComparison.Ordinal)
            || combined.Contains("PARTICIPACION DEL ASEGURADO", StringComparison.Ordinal);
    }

    private static string NormalizeDocumentText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim().ToUpperInvariant()
            .Replace('Á', 'A')
            .Replace('É', 'E')
            .Replace('Í', 'I')
            .Replace('Ó', 'O')
            .Replace('Ú', 'U')
            .Replace('Ü', 'U')
            .Replace('Ñ', 'N');
    }

    private async Task<DownloadedMetaMedia> DescargarMediaMetaAsync(WhatsAppMensaje msg)
    {
        var waConfig = await _settings.GetWhatsAppConfigAsync(_config, includeSecret: true);
        if (!waConfig.Enabled || string.IsNullOrWhiteSpace(waConfig.AccessToken))
            throw new InvalidOperationException("WhatsApp no esta configurado.");

        var version = string.IsNullOrWhiteSpace(waConfig.GraphVersion) ? "v18.0" : waConfig.GraphVersion;
        var infoReq = new HttpRequestMessage(HttpMethod.Get, $"https://graph.facebook.com/{version}/{msg.MediaId}");
        infoReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", waConfig.AccessToken);
        var infoRes = await _http.SendAsync(infoReq);
        if (!infoRes.IsSuccessStatusCode)
            throw new InvalidOperationException("No se pudo obtener el archivo desde Meta.");

        var infoJson = await infoRes.Content.ReadAsStringAsync();
        string? downloadUrl;
        string? mimeType;
        using (var doc = JsonDocument.Parse(infoJson))
        {
            downloadUrl = doc.RootElement.TryGetProperty("url", out var u) ? u.GetString() : null;
            mimeType = doc.RootElement.TryGetProperty("mime_type", out var m) ? m.GetString() : msg.MediaTipoMime;
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
            throw new InvalidOperationException("Meta no devolvio URL de descarga.");

        var dlReq = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        dlReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", waConfig.AccessToken);
        var dlRes = await _http.SendAsync(dlReq, HttpCompletionOption.ResponseHeadersRead);
        if (!dlRes.IsSuccessStatusCode)
            throw new InvalidOperationException("No se pudo descargar el archivo desde Meta.");

        var fileName = string.IsNullOrWhiteSpace(msg.MediaNombre)
            ? $"whatsapp_{msg.Id}"
            : msg.MediaNombre;
        var contentType = mimeType ?? msg.MediaTipoMime ?? "application/octet-stream";
        var stream = await dlRes.Content.ReadAsStreamAsync();
        return new DownloadedMetaMedia(stream, fileName, contentType, dlRes.Content.Headers.ContentLength);
    }
}

public record RespuestaRequest(string Mensaje);
public record EstadoRequest(string Estado);
public record AsociarClienteRequest(int ClienteId);
public record AsociarReclamoRequest(int? ReclamoId);
public record AsignarAgenteRequest(int? AgenteId);
public record GuardarDocumentoWhatsAppRequest(int ReclamoId, int ReclamoDocumentoId, string? Observacion);
public sealed record DownloadedMetaMedia(Stream Stream, string FileName, string ContentType, long? ContentLength);
public record ReclamoLinkOption(
    int Id,
    string Referencia,
    string Cliente,
    string? Poliza,
    string? Placa,
    string? Celular,
    DateTime? FechaNotificacion,
    string Estado,
    bool TelefonoCoincide);
