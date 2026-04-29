using System.Text;
using System.Text.Json;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Services.DataQuality;

namespace ReclamosWhatsApp.Services;

public class WhatsAppService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly PhoneNormalizationService _phones;

    public WhatsAppService(IConfiguration config, HttpClient http, PhoneNormalizationService phones)
    {
        _config = config;
        _http = http;
        _phones = phones;
    }

    public async Task<(bool ok, string response)> SendTemplateAsync(ReclamoWhatsApp r)
    {
        return await SendTextAsync(r.Celular ?? "", r.MensajeWhatsApp ?? "");
    }

    public async Task<(bool ok, string response)> SendTextAsync(string numeroDestino, string mensaje)
    {
        var enabled = _config.GetValue<bool>("WhatsApp:Enabled");

        if (!enabled)
            return (false, "WhatsApp no esta habilitado en appsettings.json.");

        var token = _config["WhatsApp:AccessToken"];
        var phoneId = _config["WhatsApp:PhoneNumberId"];
        var version = _config["WhatsApp:GraphVersion"] ?? "v18.0";

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(phoneId))
            return (false, "La configuracion de WhatsApp esta incompleta.");

        var normalizedPhone = NormalizePhone(numeroDestino);
        if (string.IsNullOrWhiteSpace(normalizedPhone))
            return (false, "Cliente sin telefono valido para WhatsApp.");

        if (string.IsNullOrWhiteSpace(mensaje))
            return (false, "El mensaje de WhatsApp esta vacio.");

        var url = $"https://graph.facebook.com/{version}/{phoneId}/messages";
        var body = new
        {
            messaging_product = "whatsapp",
            to = normalizedPhone,
            type = "text",
            text = new
            {
                preview_url = false,
                body = mensaje
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        return (response.IsSuccessStatusCode, responseText);
    }

    public async Task<(bool ok, string response)> NotificarAdminReclamoCompletoAsync(ReclamoWhatsApp r)
    {
        var adminNumber = _config["Admin:WhatsAppNumber"];
        if (string.IsNullOrWhiteSpace(adminNumber))
            return (false, "No esta configurado Admin:WhatsAppNumber.");

        var mensaje = $@"
Reclamo COMPLETO

Cliente / Conductor: {r.Conductor}
Celular: {r.Celular}
Asegurado: {r.Asegurado}
Poliza: {r.Poliza}
Placa: {r.Placa}
Reclamo: {r.Reclamo}
Fecha notificacion: {r.FechaNotificacion?.ToString("dd/MM/yyyy")}
Lugar: {r.LugarAccidente}

Ya se marcaron todos los documentos como recibidos.
".Trim();

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
        var mensaje = $@"
Buenas tardes {nombre}.

Le recordamos que para continuar con la gestion de su reclamo {referencia}, aun tenemos pendiente recibir:

{lista}

Por favor enviarnos esa documentacion para poder avanzar con el tramite.

Atentamente.
".Trim();

        return await SendTextAsync(r.Celular ?? "", mensaje);
    }

    public async Task<(bool ok, string response)> EnviarDocumentosRecibidosAsync(ReclamoWhatsApp r)
    {
        var nombre = string.IsNullOrWhiteSpace(r.Conductor) ? "cliente" : r.Conductor;
        var referencia = string.IsNullOrWhiteSpace(r.Reclamo) ? r.NumeroReclamo ?? $"#{r.Id}" : r.Reclamo;
        var mensaje = $@"
Buenas tardes {nombre}.

Le confirmamos que hemos recibido todos los documentos solicitados para su reclamo {referencia}.

Continuaremos con la revision y le estaremos informando cualquier avance del tramite.

Atentamente.
".Trim();

        return await SendTextAsync(r.Celular ?? "", mensaje);
    }

    private string NormalizePhone(string? value)
    {
        var normalized = _phones.NormalizeMany(value);
        return normalized.WhatsappReady ? normalized.PrincipalWhatsApp : "";
    }
}
