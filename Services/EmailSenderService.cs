using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Services;

public class EmailSenderService
{
    private readonly IConfiguration _config;
    private readonly AppSettingsRepository _settings;
    private readonly DocumentoStorageService _storage;

    public EmailSenderService(IConfiguration config, AppSettingsRepository settings, DocumentoStorageService storage)
    {
        _config = config;
        _settings = settings;
        _storage = storage;
    }

    public async Task<(bool ok, string response)> EnviarDocumentosReclamoAsync(ReclamoWhatsApp reclamo, string destino, IEnumerable<DocumentoDto> documentos, string? copia = null, bool soloComprobantesFinales = false)
    {
        if (string.IsNullOrWhiteSpace(destino) || !MailboxAddress.TryParse(destino.Trim(), out var to))
            return (false, "Ingresa un correo valido de aseguradora.");

        MailboxAddress? cc = null;
        if (!string.IsNullOrWhiteSpace(copia) && !MailboxAddress.TryParse(copia.Trim(), out cc))
            return (false, "Ingresa un correo de copia valido.");

        var docs = documentos.ToList();
        if (docs.Count == 0)
            return (false, soloComprobantesFinales
                ? "No hay comprobantes finales adjuntos para enviar a la aseguradora."
                : "No hay documentos adjuntos para enviar.");

        var smtp = await _settings.GetSmtpConfigAsync(_config);
        if (!smtp.Enabled)
            return (false, "El envio SMTP esta deshabilitado.");

        var host = smtp.Host;
        var port = smtp.Port;
        var user = smtp.Username;
        var password = smtp.Password;
        var fromAddress = string.IsNullOrWhiteSpace(smtp.FromAddress) ? user : smtp.FromAddress;
        var fromName = smtp.FromName;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fromAddress))
            return (false, "La configuracion SMTP esta incompleta.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(to);
        if (cc is not null)
            message.Cc.Add(cc);
        var referencia = reclamo.Reclamo ?? reclamo.NumeroReclamo ?? reclamo.Id.ToString();
        message.Subject = soloComprobantesFinales
            ? $"Comprobantes finales de reclamo {referencia} - deducible/RSA/coaseguro"
            : $"Expediente de reclamo {referencia} - documentos para revision";

        var adjuntos = docs.Select((doc, index) =>
        {
            var tipo = ReclamoDocumentLabel(doc.TipoDocumento);
            var observacion = string.IsNullOrWhiteSpace(doc.Observacion)
                ? ""
                : $"{Environment.NewLine}   Observacion: {doc.Observacion.Trim()}";
            return $"{index + 1}. {tipo}{Environment.NewLine}   Archivo: {doc.NombreArchivoOriginal}{observacion}";
        });

        var builder = new BodyBuilder
        {
            TextBody = $@"
Buenas tardes.

{(soloComprobantesFinales ? "Remitimos comprobantes finales recibidos del cliente para continuar con el cierre del reclamo." : "Remitimos expediente digital para revision de la aseguradora.")}

Cliente / Conductor: {reclamo.Conductor ?? reclamo.Asegurado}
Asegurado: {reclamo.Asegurado}
Poliza: {reclamo.Poliza}
Placa: {reclamo.Placa}
Reclamo: {referencia}

Documentos adjuntos:
{string.Join(Environment.NewLine, adjuntos)}

Quedamos atentos.
".Trim()
        };

        foreach (var doc in docs)
        {
            var (_, path) = await _storage.PrepararDescargaAsync(doc.Id);
            builder.Attachments.Add(doc.NombreArchivoOriginal, await File.ReadAllBytesAsync(path), ContentType.Parse(doc.MimeType));
        }

        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        var secure = port == 465 || smtp.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
        await client.ConnectAsync(host, port, secure);
        await client.AuthenticateAsync(user, password);
        var response = await client.SendAsync(message);
        await client.DisconnectAsync(true);
        return (true, response);
    }

    private static string ReclamoDocumentLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Documento";

        var normalized = value.Trim().ToUpperInvariant().Replace(' ', '_');
        return normalized switch
        {
            "FOTO_RECLAMO" => "Fotos del reclamo",
            "LICENCIA" => "Licencia del conductor",
            "TARJETA_CIRCULACION" => "Tarjeta de circulacion",
            "COTIZACION_TALLER" => "Cotizacion de taller",
            "INFORME_TALLER" => "Informe de taller",
            "FINIQUITO" => "Finiquito",
            "AVISO_ACCIDENTE" => "Aviso de accidente",
            "PAGO_DEDUCIBLE" => "Pago de deducible",
            "PAGO_RSA" => "Pago de RSA (restitucion de suma asegurada)",
            "PAGO_COASEGURO" => "Pago de coaseguro",
            _ => value.Replace('_', ' ').Trim()
        };
    }

}
