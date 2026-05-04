using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Services;

public class EmailReaderService
{
    private readonly IConfiguration _config;
    private readonly AppSettingsRepository _settings;
    private readonly ILogger<EmailReaderService> _logger;

    public EmailReaderService(IConfiguration config, AppSettingsRepository settings, ILogger<EmailReaderService> logger)
    {
        _config = config;
        _settings = settings;
        _logger = logger;
    }

    public async Task<List<EmailMessageDto>> GetUnreadEmailsAsync(int? lookbackHoursOverride = null)
    {
        var result = new List<EmailMessageDto>();
        var runtime = await _settings.GetReclamoCorreoConfigAsync(_config);
        var emailEnabled = runtime.EmailEnabled;
        if (!emailEnabled)
        {
            _logger.LogWarning("Email está deshabilitado en appsettings.json");
            return result;
        }

        // TODO production: read IMAP secrets from environment variables or a secret store.
        var host = runtime.Host;
        var port = runtime.Port;
        var user = runtime.Username;
        var password = runtime.Password;
        var useSsl = runtime.UseSsl;
        var mailbox = runtime.Mailbox ?? "INBOX";
        var markAsRead = runtime.MarkAsRead;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("La configuracion IMAP esta incompleta. Revisa host, usuario y clave.");
            return result;
        }

        _logger.LogInformation("Conectando a correo IMAP {Host}:{Port} con usuario {User}", host, port, user);

        using var client = new ImapClient();

        await client.ConnectAsync(host, port, useSsl);
        await client.AuthenticateAsync(user, password);

        _logger.LogInformation("Conectado correctamente a Gmail IMAP");

        var folder = await client.GetFolderAsync(mailbox);
        await folder.OpenAsync(FolderAccess.ReadWrite);

        _logger.LogInformation("Carpeta abierta: {Mailbox}. Total mensajes: {Count}", mailbox, folder.Count);

        var lookbackHours = lookbackHoursOverride ?? runtime.LookbackHours;
        lookbackHours = Math.Clamp(lookbackHours, 1, 24 * 30);
        var desde = DateTime.UtcNow.AddHours(-lookbackHours);
        _logger.LogInformation("Buscando correos no leidos desde hace {Hours} horas (UTC: {DesdeUtc})", lookbackHours, desde);

        var uids = await folder.SearchAsync(
            SearchQuery.DeliveredAfter(desde)
                .And(SearchQuery.NotSeen)
        );

        _logger.LogInformation("Correos no leídos encontrados en últimas 2 horas: {Count}", uids.Count);

        foreach (var uid in uids)
        {
            var message = await folder.GetMessageAsync(uid);

            _logger.LogInformation("Correo detectado: {Subject} | MessageId: {MessageId}",
                message.Subject,
                message.MessageId ?? uid.ToString());

            result.Add(new EmailMessageDto
            {
                MessageId = message.MessageId ?? uid.ToString(),
                Subject = message.Subject ?? "",
                Body = message.TextBody ?? message.HtmlBody ?? ""
            });

            if (markAsRead)
            {
                await folder.AddFlagsAsync(uid, MessageFlags.Seen, true);
            }
        }

        await client.DisconnectAsync(true);

        return result;
    }
}
