using System.Text;
using System.Text.Json;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Services.DataQuality;

namespace ReclamosWhatsApp.Services;

public class WhatsAppService
{
    private const int SingleTemplateParameterMaxLength = 900;
    private const int InitialDocumentParameterCount = 7;
    private const int ReminderDocumentParameterCount = 6;

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
                        parameters = templateParameters.Select(x => new { type = "text", text = SanitizeTemplateParameter(x) }).ToArray()
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
        var referencia = string.IsNullOrWhiteSpace(r.Reclamo) ? r.NumeroReclamo ?? $"#{r.Id}" : r.Reclamo;
        var fecha = r.FechaNotificacion?.ToString("dd/MM/yyyy") ?? "";
        var lugar = string.IsNullOrWhiteSpace(r.LugarAccidente) ? "el lugar indicado en el reclamo" : r.LugarAccidente;
        var documentos = BuildInitialDocumentLines();
        var talleres = await BuildTalleresTextAsync(r);
        var documentosTexto = string.Join(Environment.NewLine, documentos);
        var mensaje = r.MensajeWhatsApp ?? $@"Buenas tardes, {nombre}.

Reciba un cordial saludo. Le comunicamos que su reclamo {referencia} fue notificado con fecha {fecha}, ocurrido en {lugar}.

Para poder avanzar con la gestion debe completar la siguiente documentacion:

{documentosTexto}

{talleres}

Una vez se entregue completa la informacion, se evaluara la cobertura y la aplicacion de deducibles.

Atentamente.";

        return await SendWhatsAppTemplateAsync(
            r.Celular ?? "",
            config.ReclamoInitialTemplateName,
            config.LanguageCode,
            config,
            UsesCurrentApprovedInitialTemplate(config.ReclamoInitialTemplateName)
                ? BuildFixedTemplateParameters(
                    [nombre, fecha, lugar],
                    documentos,
                    InitialDocumentParameterCount,
                    ["No aplica", talleres],
                    2)
                : UsesExpandedClaimTemplate(config.ReclamoInitialTemplateName)
                ? BuildFixedTemplateParameters(
                    [nombre, referencia, fecha, lugar],
                    documentos,
                    InitialDocumentParameterCount,
                    [talleres],
                    1)
                : [nombre, fecha, lugar, documentosTexto, talleres],
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
        var documentos = pendientes.Select((doc, index) => $"{index + 1}. {doc}").ToList();
        var lista = string.Join(Environment.NewLine, documentos);
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
                UsesExpandedClaimTemplate(config.ReclamoReminderTemplateName)
                    ? BuildReminderTemplateParameters(nombre, referencia, documentos)
                    : [nombre, referencia, lista],
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

    private async Task<string> BuildTalleresTextAsync(ReclamoWhatsApp r)
    {
        var ciudad = HondurasLocationService.DetectCity(r.LugarAccidente);
        if (HondurasLocationService.IsTegucigalpa(ciudad))
        {
            return DefaultTalleresServicioClienteText;
        }

        var talleres = (await _talleres.SugerirAsync(ciudad, r.Aseguradora, r.TipoReclamo ?? "AUTOS")).Take(5).ToList();
        if (talleres.Count > 0)
        {
            return "Talleres en red: " + string.Join(" | ", talleres.Select(x => $"{x.Nombre}: {x.Direccion}{(string.IsNullOrWhiteSpace(x.Telefono) ? "" : $" / Tel. {x.Telefono}")}"));
        }

        return DefaultTalleresServicioClienteText;
    }

    private const string DefaultTalleresServicioClienteText =
        "Talleres en red: para informacion de talleres, por favor contacte a servicio al cliente al numero 89659690. Con gusto le apoyaremos para coordinar la atencion que corresponda con la aseguradora.";

    private static List<string> BuildInitialDocumentLines()
    {
        var documents = new[]
        {
            "Aviso de accidente original firmado por el conductor y asegurado. Si es empresa, aplicar sello correspondiente. Puede compartir este documento (formulario aseguradora) al numero de servicio al cliente 89659690 para gestionar firma y sello, y agilizar el tramite con la aseguradora.",
            "Certificacion de Transito.",
            "Tarjeta de identidad del conductor, ambos lados.",
            "Licencia del conductor, ambos lados.",
            "Boleta de circulacion del vehiculo asegurado.",
            "Inspeccion puntual de danos (opcional, solo si la aseguradora la solicita).",
            "Dos cotizaciones de talleres de la red, cuando aplique."
        };

        return documents.Select((document, index) => $"{index + 1}. {document}").ToList();
    }

    private static string[] BuildFixedTemplateParameters(
        IEnumerable<string> headerParameters,
        IEnumerable<string> variableItems,
        int itemParameterCount)
    {
        return BuildFixedTemplateParameters(headerParameters, variableItems, itemParameterCount, [], 0);
    }

    private static string[] BuildFixedTemplateParameters(
        IEnumerable<string> headerParameters,
        IEnumerable<string> firstItems,
        int firstItemParameterCount,
        IEnumerable<string> secondItems,
        int secondItemParameterCount)
    {
        var parameters = new List<string>();
        parameters.AddRange(headerParameters.Select(TemplateBlankSafe));
        parameters.AddRange(PadTemplateItems(firstItems, firstItemParameterCount));
        parameters.AddRange(PadTemplateItems(secondItems, secondItemParameterCount));
        return parameters.ToArray();
    }

    private static IEnumerable<string> PadTemplateItems(IEnumerable<string> items, int count)
    {
        var normalized = items.Select(TemplateBlankSafe).Take(count).ToList();
        while (normalized.Count < count)
            normalized.Add("No aplica");

        return normalized;
    }

    private static string[] BuildReminderTemplateParameters(string nombre, string referencia, IEnumerable<string> documentos)
    {
        var parameters = new List<string>
        {
            TemplateBlankSafe(nombre),
            TemplateBlankSafe(referencia)
        };

        var documentItems = documentos.Select(TemplateBlankSafe).Take(ReminderDocumentParameterCount).ToList();
        while (documentItems.Count < ReminderDocumentParameterCount)
            documentItems.Add($"{documentItems.Count + 1}. Documento ya recibido.");

        parameters.AddRange(documentItems);
        return parameters.ToArray();
    }

    private static string TemplateBlankSafe(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "No aplica" : value.Trim();
    }

    private static bool UsesExpandedClaimTemplate(string? templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            return false;

        return templateName.Contains("_v2", StringComparison.OrdinalIgnoreCase);
    }

    private static bool UsesCurrentApprovedInitialTemplate(string? templateName)
    {
        return string.Equals(templateName, "params_reclamo_documentos_inicial_es", StringComparison.OrdinalIgnoreCase);
    }

    private string NormalizePhone(string? value)
    {
        var normalized = _phones.NormalizeMany(value);
        return normalized.WhatsappReady ? normalized.PrincipalWhatsApp : "";
    }

    private static IEnumerable<string> SplitTemplateParameter(string? value, int maxLength)
    {
        var text = SanitizeTemplateParameter(value);
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

    private static string SanitizeTemplateParameter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var text = value
            .Replace("\r\n", " ")
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ')
            .Trim();

        while (text.Contains("     ", StringComparison.Ordinal))
            text = text.Replace("     ", "    ", StringComparison.Ordinal);

        return text;
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
