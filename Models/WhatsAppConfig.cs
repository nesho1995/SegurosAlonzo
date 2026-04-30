namespace ReclamosWhatsApp.Models;

public class WhatsAppConfig
{
    public bool Enabled { get; set; }
    public string GraphVersion { get; set; } = "v18.0";
    public string PhoneNumberId { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string AccessTokenMasked { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string LanguageCode { get; set; } = "es";
    public string AdminWhatsAppNumber { get; set; } = "";
}

public class WhatsAppTestRequest
{
    public string? Telefono { get; set; }
    public string? Mensaje { get; set; }
}
