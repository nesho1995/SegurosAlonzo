namespace ReclamosWhatsApp.Models;

public class PolizaCuota
{
    public int Id { get; set; }
    public int PolizaId { get; set; }
    public int ClienteId { get; set; }
    public string Cliente { get; set; } = "";
    public string? Telefono { get; set; }
    public string? NumeroPoliza { get; set; }
    public string? Aseguradora { get; set; }
    public string? Ramo { get; set; }
    public int NumeroCuota { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public decimal Monto { get; set; }
    public decimal MontoPagado { get; set; }
    public string Estado { get; set; } = "PENDIENTE";
    public DateTime? FechaPago { get; set; }
    public string? MetodoPago { get; set; }
    public string? ComprobanteUrl { get; set; }
    public int? DocumentoId { get; set; }
    public string? NumeroRecibo { get; set; }
    public string? ReferenciaBanco { get; set; }
    public string? Observaciones { get; set; }
    public DateTime FechaCreacion { get; set; }
}
