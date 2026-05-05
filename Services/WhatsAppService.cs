using System.Text;
using System.Text.Json;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Services.DataQuality;

namespace ReclamosWhatsApp.Services;

public class WhatsAppService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly PhoneNormalizationService _phones;
    private readonly AppSettingsRepository _settings;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(
        IConfiguration config,
        HttpClient http,
        PhoneNormalizationService phones,
        AppSettingsRepository settings,
        ILogger<WhatsAppService> logger)
    {
        _config = config;
        _http = http;
        _phones = phones;
        _settings = settings;
        _logger = logger;
    }

    // ─── Envío principal ─────────────────────────────────────────────────────
    // Usa plantilla si TemplateName está configurado, si no texto libre.
    // IMPORTANTE: Meta solo permite texto libre dentro de la ventana de 24h
    // después de que el cliente haya enviado un mensaje. Para notificaciones
    // iniciadas por el sistema SIEMPRE se necesita una plantilla aprobada.

    public async Task<(bool ok, string response)> SendTemplateAsync(ReclamoWhatsApp r)
    {
        var config = await _settings.GetWhatsAppConfigAsync(_config, includeSecret: true);

        if (!string.IsNullOrWhiteSpace(config.TemplateName))
            return await SendWhatsAppTemplateAsync(r.Celular ?? "", config.TemplateName, config.LanguageCode,
                config, r.MensajeWhatsApp ?? "");

        // Fallback: texto libre (solo dentro de ventana 24h)
        return await SendTextAsync(r.Celular ?? "", r.MensajeWhatsApp ?? "", config);
    }

    public async Task<(bool ok, string response)> SendTextAsync(string numeroDestino, string mensaje)
    {
        var config = await _settings.GetWhatsAppConfigAsync(_config, includeSecret: true);
        return await SendTextAsync(numeroDestino, mensaje, config);
    }

    // ─── Internos ────────────────────────────────────────────────────────────

    private async Task<(bool ok, string response)> SendTextAsync(string numeroDestino, string mensaje, WhatsAppConfig config)
    {
        if (!config.Enabled)
            return (false, "WhatsApp está desactivado desde Configuración → WhatsApp.");

        if (string.IsNullOrWhiteSpace(config.AccessToken) || string.IsNullOrWhiteSpace(config.PhoneNumberId))
            return (false, "La configuración de WhatsApp está incompleta (token o Phone Number ID).");

        var normalizedPhone = NormalizePhone(numeroDestino);
        if (string.IsNullOrWhiteSpace(normalizedPhone))
            return (false, "Cliente sin teléfono válido para WhatsApp.");

        if (string.IsNullOrWhiteSpace(mensaje))
            return (false, "El mensaje de WhatsApp está vacío.");

        var body = new
        {
            messaging_product = "whatsapp",
            to = normalizedPhone,
            type = "text",
            text = new { preview_url = false, body = mensaje }
        };

        return await PostToMetaAsync(config, body);
    }

    /// <summary>
    /// Envía un mensaje usando una plantilla aprobada en Meta.
    /// La plantilla debe tener un único parámetro {{1}} en el body
    /// que recibirá el texto completo del mensaje.
    /// </summary>
    private async Task<(bool ok, string response)> SendWhatsAppTemplateAsync(
        string numeroDestino,
        string templateName,
        string languageCode,
        WhatsAppConfig config,
        string mensajeTexto)
    {
        if (!config.Enabled)
            return (false, "WhatsApp está desactivado desde Configuración → WhatsApp.");

        if (string.IsNullOrWhiteSpace(config.AccessToken) || string.IsNullOrWhiteSpace(config.PhoneNumberId))
            return (false, "La configuración de WhatsApp está incompleta (token o Phone Number ID).");

        var normalizedPhone = NormalizePhone(numeroDestino);
        if (string.IsNullOrWhiteSpace(normalizedPhone))
            return (false, "Cliente sin teléfono válido para WhatsApp.");

        var body = new
        {
            messaging_product = "whatsapp",
            to = normalizedPhone,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = string.IsNullOrWhiteSpace(languageCode) ? "es" : languageCode },
                components = new[]
                {
                    new
                    {
                        type = "body",
                        parameters = new[]
                        {
                            new { type = "text", text = mensajeTexto }
                        }
                    }
                }
            }
        };

        return await PostToMetaAsync(config, body);
    }

    private async Task<(bool ok, string response)> PostToMetaAsync(WhatsAppConfig config, object body)
    {
        var version = string.IsNullOrWhiteSpace(config.GraphVersion) ? "v18.0" : config.GraphVersion;
        var url = $"https://graph.facebook.com/{version}/{config.PhoneNumberId}/messages";

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.AccessToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error HTTP al enviar WhatsApp");
            return (false, "No se pudo conectar con Meta. Verifica la conexión a internet.");
        }

        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            // Parsear error de Meta para mostrar mensaje útil
            try
            {
                using var doc = JsonDocument.Parse(responseText);
                var error = doc.RootElement.GetProperty("error");
                var msg = error.TryGetProperty("message", out var m) ? m.GetString() : null;
                var code = error.TryGetProperty("code", out var c) ? c.GetInt32().ToString() : "";
                _logger.LogWarning("Meta WhatsApp error {Code}: {Msg}", code, msg);
                return (false, $"Error Meta ({code}): {msg ?? responseText}");
            }
            catch
            {
                return (false, responseText);
            }
        }

        return (true, responseText);
    }

    // ─── Mensajes específicos ─────────────────────────────────────────────────

    public async Task<(bool ok, string response)> NotificarAdminReclamoCompletoAsync(ReclamoWhatsApp r)
    {
        var config = await _settings.GetWhatsAppConfigAsync(_config);
        var adminNumber = config.AdminWhatsAppNumber;
        if (string.IsNullOrWhiteSpace(adminNumber))
            return (false, "No está configurado el número administrador de WhatsApp.");

        var mensaje = $@"Reclamo COMPLETO

Cliente / Conductor: {r.Conductor}
Celular: {r.Celular}
Asegurado: {r.Asegurado}
Póliza: {r.Poliza}
Placa: {r.Placa}
Reclamo: {r.Reclamo}
Fecha notificación: {r.FechaNotificacion?.ToString("dd/MM/yyyy")}
Lugar: {r.LugarAccidente}

Ya se marcaron todos los documentos como recibidos.".Trim();

        return await SendTextAsync(adminNumber, mensaje);
    }

    public async Task<(bool ok, string response)> EnviarRecordatorioAsync(ReclamoWhatsApp r, IEnumerable<ReclamoDocumento>? documentosPendientes = null)
    {
        var pendientes = (documentosPendientes ?? [])
            .Where(x => !x.Recibido)
            .Select(x => x.Documento)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (pendientes.Count == 0)
            return (false, "No hay documentos pendientes para solicitar al cliente.");

        var nombre = string.IsNullOrWhiteSpace(r.Conductor) ? "cliente" : r.Conductor;
        var referencia = string.IsNullOrWhiteSpace(r.Reclamo) ? r.NumeroReclamo ?? $"#{r.Id}" : r.Reclamo;
        var lista = string.Join(Environment.NewLine, pendientes.Select((doc, index) => $"{index + 1}. {doc}"));
        var mensaje = $@"Buenas tardes {nombre}.

Le recordamos que para continuar con la gestión de su reclamo {referencia}, aún tenemos pendiente recibir:

{lista}

Por favor enviarnos esa documentación para poder avanzar con el trámite.

Atentamente.".Trim();

        return await SendTextAsync(r.Celular ?? "", mensaje);
    }

    public async Task<(bool ok, string response)> EnviarDocumentosRecibidosAsync(ReclamoWhatsApp r)
    {
        var nombre = string.IsNullOrWhiteSpace(r.Conductor) ? "cliente" : r.Conductor;
        var referencia = string.IsNullOrWhiteSpace(r.Reclamo) ? r.NumeroReclamo ?? $"#{r.Id}" : r.Reclamo;
        var mensaje = $@"Buenas tardes {nombre}.

Le confirmamos que hemos recibido todos los documentos solicitados para su reclamo {referencia}.

Continuaremos con la revisión y le estaremos informando cualquier avance del trámite.

Atentamente.".Trim();

        return await SendTextAsync(r.Celular ?? "", mensaje);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private string NormalizePhone(string? value)
    {
        var normalized = _phones.NormalizeMany(value);
        return normalized.WhatsappReady ? normalized.PrincipalWhatsApp : "";
    }
}
