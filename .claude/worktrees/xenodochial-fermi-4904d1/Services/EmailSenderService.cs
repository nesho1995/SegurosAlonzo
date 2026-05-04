using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Services;

public class EmailSenderService
{
    private readonly IConfiguration _config;
    private readonly DocumentoStorageService _storage;

    public EmailSenderService(IConfiguration config, DocumentoStorageService storage)
    {
        _config = config;
        _storage = storage;
    }

    public async Task<(bool ok, string response)> EnviarDocumentosReclamoAsync(ReclamoWhatsApp reclamo, string destino, IEnumerable<DocumentoDto> documentos)
    {
        if (string.IsNullOrWhiteSpace(destino) || !MailboxAddress.TryParse(destino.Trim(), out var to))
            return (false, "Ingresa un correo valido de aseguradora.");

        var docs = documentos.ToList();
        if (docs.Count == 0)
            return (false, "No hay documentos adjuntos para enviar.");

        var host = _config["Smtp:Host"] ?? _config["Email:SmtpHost"] ?? "smtp.gmail.com";
        var port = GetInt("Smtp:Port", GetInt("Email:SmtpPort", 587));
        var user = _config["Smtp:User"] ?? _config["Email:User"] ?? "";
        var password = _config["Smtp:Password"] ?? _config["Email:Password"] ?? "";
        var fromAddress = _config["Smtp:From"] ?? user;
        var fromName = _config["Smtp:FromName"] ?? "Reclamos";

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fromAddress))
            return (false, "La configuracion SMTP esta incompleta.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(to);
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
        var secure = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
        await client.ConnectAsync(host, port, secure);
        await client.AuthenticateAsync(user, password);
        var response = await client.SendAsync(message);
        await client.DisconnectAsync(true);
        return (true, response);
    }

    private int GetInt(string key, int fallback)
    {
        return int.TryParse(_config[key], out var value) && value > 0 ? value : fallback;
    }
}
