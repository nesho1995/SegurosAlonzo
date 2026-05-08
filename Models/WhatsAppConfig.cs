namespace ReclamosWhatsApp.Models;

public class WhatsAppConfig
{
    public bool Enabled { get; set; }
    public string GraphVersion { get; set; } = "v18.0";
    public string PhoneNumberId { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string AccessTokenMasked { get; set; } = "";
    /// <summary>
    /// Nombre de la plantilla aprobada en Meta Business Manager con un solo parametro {{1}}.
    /// Si está vacío se envía texto libre (solo funciona dentro de la ventana de 24h).
    /// </summary>
    public string TemplateName { get; set; } = "";
    public string ReclamoInitialTemplateName { get; set; } = "";
    public string ReclamoReminderTemplateName { get; set; } = "";
    public string ReclamoCompleteTemplateName { get; set; } = "";
    public string LanguageCode { get; set; } = "es";
    public string AdminWhatsAppNumber { get; set; } = "";
    /// <summary>
    /// Token secreto que Meta usará para verificar el webhook.
    /// Configúralo en Meta Business Manager → WhatsApp → Configuration → Webhook.
    /// </summary>
    public string WebhookVerifyToken { get; set; } = "";
    public string WebhookVerifyTokenMasked { get; set; } = "";
}

public class WhatsAppTestRequest
{
    public string? Telefono { get; set; }
    public string? Mensaje { get; set; }
}
