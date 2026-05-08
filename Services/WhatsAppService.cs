using System.Text;
using System.Text.Json;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Services.DataQuality;

namespace ReclamosWhatsApp.Services;

public class WhatsAppService
{
    private const int SingleTemplateParameterMaxLength = 900;

    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly PhoneNormalizationService _phones;
    private readonly AppSettingsRepository _settings;
    private readonly WhatsAppConversacionRepository _conversaciones;
    private readonly TallerRepository _talleres;
    private readonly EmpresaConfiguracionRepository _empresa;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(
        IConfiguration config,
        HttpClient http,
        PhoneNormalizationService phones,
        AppSettingsRepository settings,
        WhatsAppConversacionRepository conversaciones,
        TallerRepository talleres,
        EmpresaConfiguracionRepository empresa,
        ILogger<WhatsAppService> logger)
    {
        _config = config;
        _http = http;
        _phones = phones;
        _settings = settings;
        _conversaciones = conversaciones;
        _talleres = talleres;
        _empresa = empresa;
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

        if (!string.IsNullOrWhiteSpace(config.ReclamoInitialTemplateName))
            return await SendReclamoInitialTemplateAsync(r, config);

        if (!string.IsNullOrWhiteSpace(config.TemplateName))
            return await SendWhatsAppTemplateAsync(r.Celular ?? "", config.TemplateName, config.LanguageCode,
                config, r.MensajeWhatsApp ?? "");

        // Fallback: texto libre (solo dentro de ventana 24h)
        return await SendTextAsync(r.Celular ?? "", r.MensajeWhatsApp ?? "", config);
    }

    public async Task<(bool ok, string response)> SendConfiguredMessageAsync(
        string numeroDestino,
        string mensaje,
        int? usuarioId = null)
    {
        var config = await _settings.GetWhatsAppConfigAsync(_config, includeSecret: true);

        if (!string.IsNullOrWhiteSpace(config.TemplateName))
            return await SendWhatsAppTemplateAsync(
                numeroDestino,
                config.TemplateName,
                config.LanguageCode,
                config,
                mensaje,
                usuarioId);

        return await SendTextAsync(numeroDestino, mensaje, config, usuarioId);
    }

    public async Task<(bool ok, string response)> SendTextAsync(string numeroDestino, string mensaje, int? usuarioId = null)
    {
        var config = await _settings.GetWhatsAppConfigAsync(_config, includeSecret: true);
        return await SendTextAsync(numeroDestino, mensaje, config, usuarioId);
    }

    // ─── Internos ────────────────────────────────────────────────────────────

    private async Task<(bool ok, string response)> SendTextAsync(
        string numeroDestino, string mensaje, WhatsAppConfig config, int? usuarioId = null)
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

        var (ok, response) = await PostToMetaAsync(config, body);

        if (ok)
        {
            try
            {
                await SaveOutgoingMessageAsync(normalizedPhone, mensaje, response, usuarioId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo guardar mensaje saliente en bandeja para {Tel}", normalizedPhone);
            }
        }

        return (ok, response);
    }

    private async Task<(bool ok, string response)> SendWhatsAppTemplateAsync(
        string numeroDestino,
        string templateName,
        string languageCode,
        WhatsAppConfig config,
        string mensajeTexto,
        int? usuarioId = null)
    {
        var chunks = SplitTemplateParameter(mensajeTexto, SingleTemplateParameterMaxLength).ToList();
        if (chunks.Count <= 1)
        {
            return await SendWhatsAppTemplateAsync(
                numeroDestino,
                templateName,
                languageCode,
                config,
                [mensajeTexto],
                mensajeTexto,
                usuarioId);
        }

        var responses = new List<string>();
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var result = await SendWhatsAppTemplateAsync(
                numeroDestino,
                templateName,
                languageCode,
                config,
                [chunk],
                chunk,
                usuarioId);

            responses.Add(result.response);
            if (!result.ok)
                return (false, $"Parte {i + 1}/{chunks.Count}: {result.response}");
        }

        return (true, $"Mensaje enviado en {chunks.Count} partes. {string.Join(" | ", responses)}");
    }

    private async Task<(bool ok, string response)> SendWhatsAppTemplateAsync(
        string numeroDestino,
        string templateName,
        string languageCode,
        WhatsAppConfig config,
        IEnumerable<string> templateParameters,
        string mensajeGuardado,
        int? usuarioId = null)
    {
        if (!config.Enabled)
            return (false, "WhatsApp esta desactivado desde Configuracion -> WhatsApp.");

        if (string.IsNullOrWhiteSpace(config.AccessToken) || string.IsNullOrWhiteSpace(config.PhoneNumberId))
            return (false, "La configuracion de WhatsApp esta incompleta (token o Phone Number ID).");

        var normalizedPhone = NormalizePhone(numeroDestino);
        if (string.IsNullOrWhiteSpace(normalizedPhone))
            return (false, "Cliente sin telefono valido para WhatsApp.");

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
                        parameters = templateParameters.Select(x => new { type = "text", text = x ?? "" }).ToArray()
                    }
                }
            }
        };

        var (ok, response) = await PostToMetaAsync(config, body);

        if (ok)
        {
            try
            {
                await SaveOutgoingMessageAsync(normalizedPhone, mensajeGuardado, response, usuarioId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo guardar mensaje saliente de plantilla en bandeja para {Tel}", normalizedPhone);
            }
        }

        return (ok, response);
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

    private async Task<(bool ok, string response)> SendReclamoInitialTemplateAsync(ReclamoWhatsApp r, WhatsAppConfig config)
    {
        var nombre = string.IsNullOrWhiteSpace(r.Conductor) ? "cliente" : r.Conductor;
        var fecha = r.FechaNotificacion?.ToString("dd/MM/yyyy") ?? "";
        var lugar = string.IsNullOrWhiteSpace(r.LugarAccidente) ? "el lugar indicado en el reclamo" : r.LugarAccidente;
        var documentos = @"1. Aviso de accidente original firmado por el conductor y asegurado. Si es empresa, aplicar sello correspondiente.
2. Certificacion de Transito.
3. Tarjeta de identidad del conductor, ambos lados.
4. Licencia del conductor, ambos lados.
5. Boleta de circulacion del vehiculo asegurado.
6. Inspeccion puntual de danos en Seguros Crefisa.
7. Dos cotizaciones de talleres de la red, cuando aplique.
8. Estar al dia con el pago de primas del seguro.";

        var talleres = await BuildTalleresBlockAsync(r);
        var mensaje = r.MensajeWhatsApp ?? $@"Buenas tardes, {nombre}.

Reciba un cordial saludo. Le comunicamos que su reclamo fue notificado con fecha {fecha}, ocurrido en {lugar}.

Para poder avanzar con la gestion debe completar la siguiente documentacion:

{documentos}

{talleres}

Una vez se entregue completa la informacion, se evaluara la cobertura y la aplicacion de deducibles.

Atentamente.";

        return await SendWhatsAppTemplateAsync(
            r.Celular ?? "",
            config.ReclamoInitialTemplateName,
            config.LanguageCode,
            config,
            [nombre, fecha, lugar, documentos, talleres],
            mensaje);
    }

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

        return await SendConfiguredMessageAsync(adminNumber, mensaje);
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

        var config = await _settings.GetWhatsAppConfigAsync(_config, includeSecret: true);
        if (!string.IsNullOrWhiteSpace(config.ReclamoReminderTemplateName))
        {
            return await SendWhatsAppTemplateAsync(
                r.Celular ?? "",
                config.ReclamoReminderTemplateName,
                config.LanguageCode,
                config,
                [nombre, referencia, lista],
                mensaje);
        }

        return await SendConfiguredMessageAsync(r.Celular ?? "", mensaje);
    }

    public async Task<(bool ok, string response)> EnviarDocumentosRecibidosAsync(ReclamoWhatsApp r)
    {
        var nombre = string.IsNullOrWhiteSpace(r.Conductor) ? "cliente" : r.Conductor;
        var referencia = string.IsNullOrWhiteSpace(r.Reclamo) ? r.NumeroReclamo ?? $"#{r.Id}" : r.Reclamo;
        var mensaje = $@"Buenas tardes {nombre}.

Le confirmamos que hemos recibido todos los documentos solicitados para su reclamo {referencia}.

Continuaremos con la revisión y le estaremos informando cualquier avance del trámite.

Atentamente.".Trim();

        var config = await _settings.GetWhatsAppConfigAsync(_config, includeSecret: true);
        if (!string.IsNullOrWhiteSpace(config.ReclamoCompleteTemplateName))
        {
            return await SendWhatsAppTemplateAsync(
                r.Celular ?? "",
                config.ReclamoCompleteTemplateName,
                config.LanguageCode,
                config,
                [nombre, referencia],
                mensaje);
        }

        return await SendConfiguredMessageAsync(r.Celular ?? "", mensaje);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private async Task<string> BuildTalleresBlockAsync(ReclamoWhatsApp r)
    {
        var ciudad = DetectarCiudad(r.LugarAccidente);
        var talleres = (await _talleres.SugerirAsync(ciudad, r.Aseguradora, r.TipoReclamo ?? "AUTOS")).Take(5).ToList();
        if (talleres.Count > 0)
        {
            return $@"Talleres en red:
{string.Join(Environment.NewLine, talleres.Select(x => $"- {x.Nombre}: {x.Direccion}{(string.IsNullOrWhiteSpace(x.Telefono) ? "" : $" / Tel. {x.Telefono}")}"))}";
        }

        var empresa = await _empresa.GetAsync();
        return $@"Para coordinacion de taller o inspeccion, puede contactarse con la aseguradora o con nosotros para indicarle el proceso correspondiente.{(string.IsNullOrWhiteSpace(empresa.TelefonoEmpresa) ? "" : $"{Environment.NewLine}Telefono empresa: {empresa.TelefonoEmpresa}")}";
    }

    private static string? DetectarCiudad(string? lugar)
    {
        if (string.IsNullOrWhiteSpace(lugar))
            return null;

        if (lugar.Contains("TEGUCIGALPA", StringComparison.OrdinalIgnoreCase)
            || lugar.Contains("TGU", StringComparison.OrdinalIgnoreCase)
            || lugar.Contains("FRANCISCO MORAZAN", StringComparison.OrdinalIgnoreCase))
            return "TEGUCIGALPA";

        if (lugar.Contains("SAN PEDRO SULA", StringComparison.OrdinalIgnoreCase)
            || lugar.Contains("S.P.S", StringComparison.OrdinalIgnoreCase)
            || lugar.Contains("CORTES", StringComparison.OrdinalIgnoreCase))
            return "SAN PEDRO SULA";

        return lugar.Trim();
    }

    private string NormalizePhone(string? value)
    {
        var normalized = _phones.NormalizeMany(value);
        return normalized.WhatsappReady ? normalized.PrincipalWhatsApp : "";
    }

    private static IEnumerable<string> SplitTemplateParameter(string? value, int maxLength)
    {
        var text = (value ?? "").Trim();
        if (text.Length <= maxLength)
        {
            yield return text;
            yield break;
        }

        var remaining = text;
        while (remaining.Length > maxLength)
        {
            var splitAt = remaining.LastIndexOf("\n\n", maxLength, StringComparison.Ordinal);
            if (splitAt < maxLength / 2)
                splitAt = remaining.LastIndexOf('\n', maxLength);
            if (splitAt < maxLength / 2)
                splitAt = remaining.LastIndexOf(' ', maxLength);
            if (splitAt < maxLength / 2)
                splitAt = maxLength;

            yield return remaining[..splitAt].Trim();
            remaining = remaining[splitAt..].Trim();
        }

        if (!string.IsNullOrWhiteSpace(remaining))
            yield return remaining;
    }

    private async Task SaveOutgoingMessageAsync(string normalizedPhone, string mensaje, string metaResponse, int? usuarioId)
    {
        var convId = await _conversaciones.GetOrCreateConversacionAsync(normalizedPhone, null);
        await _conversaciones.SaveMensajeAsync(new WhatsAppMensaje
        {
            ConversacionId = convId,
            WhatsappMessageId = ExtractWhatsAppMessageId(metaResponse),
            Direccion = "saliente",
            TipoContenido = "texto",
            Contenido = mensaje,
            Estado = "enviado",
            UsuarioId = usuarioId
        });
    }

    private static string? ExtractWhatsAppMessageId(string response)
    {
        try
        {
            using var doc = JsonDocument.Parse(response);
            if (!doc.RootElement.TryGetProperty("messages", out var msgs))
                return null;

            var first = msgs.EnumerateArray().FirstOrDefault();
            return first.TryGetProperty("id", out var mid) ? mid.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
