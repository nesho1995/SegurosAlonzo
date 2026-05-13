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

    public async Task<(bool ok, string response)> EnviarDocumentosReclamoAsync(ReclamoWhatsApp reclamo, string destino, IEnumerable<DocumentoDto> documentos, string? copia = null)
    {
        if (string.IsNullOrWhiteSpace(destino) || !MailboxAddress.TryParse(destino.Trim(), out var to))
            return (false, "Ingresa un correo valido de aseguradora.");

        MailboxAddress? cc = null;
        if (!string.IsNullOrWhiteSpace(copia) && !MailboxAddress.TryParse(copia.Trim(), out cc))
            return (false, "Ingresa un correo de copia valido.");

        var docs = documentos.ToList();
        if (docs.Count == 0)
            return (false, "No hay documentos adjuntos para enviar.");

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
        message.Subject = $"Documentos de reclamo {reclamo.Reclamo ?? reclamo.NumeroReclamo ?? reclamo.Id.ToString()}";

        var builder = new BodyBuilder
        {
            TextBody = $@"
Buenas tardes.

Adjuntamos documentos recibidos para el reclamo:

Cliente / Conductor: {reclamo.Conductor ?? reclamo.Asegurado}
Poliza: {reclamo.Poliza}
Placa: {reclamo.Placa}
Reclamo: {reclamo.Reclamo ?? reclamo.NumeroReclamo}

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

}
