using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Security;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ReclamosWhatsApp.Controllers.Api;

/// <summary>
/// Proxy para descargar media de Meta (imágenes, documentos, audio, video).
/// Meta requiere autenticación Bearer para acceder a sus CDN, por lo que el
/// navegador no puede cargar los archivos directamente.
/// </summary>
[ApiController]
[Route("api/whatsapp/media")]
[Authorize(Policy = Permissions.WhatsAppBandejaVer)]
public class WhatsAppMediaApiController : ControllerBase
{
    private readonly AppSettingsRepository _settings;
    private readonly IConfiguration _config;
    private readonly DbConnectionFactory _db;
    private readonly HttpClient _http;
    private readonly ILogger<WhatsAppMediaApiController> _logger;

    public WhatsAppMediaApiController(
        AppSettingsRepository settings,
        IConfiguration config,
        DbConnectionFactory db,
        HttpClient http,
        ILogger<WhatsAppMediaApiController> logger)
    {
        _settings = settings;
        _config = config;
        _db = db;
        _http = http;
        _logger = logger;
    }

    // GET /api/whatsapp/media/{mediaId}
    // Devuelve el archivo multimedia directamente con su Content-Type correcto.
    // Parámetro ?download=true para forzar descarga (útil en documentos).
    [HttpGet("{mediaId}")]
    [ResponseCache(Duration = 300)] // cachear 5min, los archivos no cambian
    public async Task<IActionResult> GetMedia(string mediaId, [FromQuery] bool download = false)
    {
        // Validar que el mediaId existe en nuestra base de datos (prevenir SSRF)
        using var cn = _db.CreateConnection();
        var mime = await cn.ExecuteScalarAsync<string?>(
            "SELECT media_tipo_mime FROM whatsapp_mensajes WHERE media_id = @mediaId LIMIT 1",
            new { mediaId });

        if (mime == null)
            return NotFound(new { error = "Archivo no encontrado." });

        var waConfig = await _settings.GetWhatsAppConfigAsync(_config, includeSecret: true);
        if (!waConfig.Enabled || string.IsNullOrWhiteSpace(waConfig.AccessToken))
            return StatusCode(503, new { error = "WhatsApp no está configurado." });

        var version = string.IsNullOrWhiteSpace(waConfig.GraphVersion) ? "v18.0" : waConfig.GraphVersion;

        try
        {
            // Paso 1: obtener la URL de descarga desde Meta
            var infoReq = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://graph.facebook.com/{version}/{mediaId}");
            infoReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", waConfig.AccessToken);

            var infoRes = await _http.SendAsync(infoReq);
            if (!infoRes.IsSuccessStatusCode)
            {
                _logger.LogWarning("Meta media info falló {Status} para {Id}", infoRes.StatusCode, mediaId);
                return StatusCode(502, new { error = "No se pudo obtener el archivo de Meta." });
            }

            var infoJson = await infoRes.Content.ReadAsStringAsync();
            string? downloadUrl;
            string? mimeType;
            using (var doc = JsonDocument.Parse(infoJson))
            {
                downloadUrl = doc.RootElement.TryGetProperty("url", out var u) ? u.GetString() : null;
                mimeType = doc.RootElement.TryGetProperty("mime_type", out var m) ? m.GetString() : mime;
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
                return StatusCode(502, new { error = "Meta no devolvió URL de descarga." });

            // Paso 2: descargar el archivo real con Bearer auth
            var dlReq = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            dlReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", waConfig.AccessToken);

            var dlRes = await _http.SendAsync(dlReq, HttpCompletionOption.ResponseHeadersRead);
            if (!dlRes.IsSuccessStatusCode)
                return StatusCode(502, new { error = "No se pudo descargar el archivo." });

            var contentType = mimeType ?? "application/octet-stream";
            var stream = await dlRes.Content.ReadAsStreamAsync();

            if (download)
            {
                var nombre = await cn.ExecuteScalarAsync<string?>(
                    "SELECT media_nombre FROM whatsapp_mensajes WHERE media_id = @mediaId LIMIT 1",
                    new { mediaId }) ?? $"archivo_{mediaId}";
                Response.Headers["Content-Disposition"] = $"attachment; filename=\"{nombre}\"";
            }

            return File(stream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error descargando media {Id}", mediaId);
            return StatusCode(502, new { error = "Error al obtener el archivo." });
        }
    }
}
