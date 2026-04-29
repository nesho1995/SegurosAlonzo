using System.ComponentModel.DataAnnotations;

namespace ReclamosWhatsApp.Models;

public class Recordatorio
{
    public int Id { get; set; }
    public string Tipo { get; set; } = "";
    public string Referencia { get; set; } = "";
    public int ClienteId { get; set; }
    public int? PolizaId { get; set; }
    public int? CuotaId { get; set; }
    public string Cliente { get; set; } = "";
    public string? Telefono { get; set; }
    public string? NumeroPoliza { get; set; }
    public string? Aseguradora { get; set; }
    public string? Ramo { get; set; }
    public DateTime? FechaObjetivo { get; set; }

    [Required]
    public string Asunto { get; set; } = "";

    [Required]
    public string Mensaje { get; set; } = "";

    public string Estado { get; set; } = "PENDIENTE";
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaEnvio { get; set; }
    public string? Error { get; set; }
}
