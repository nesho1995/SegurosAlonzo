namespace ReclamosWhatsApp.Models;

public class PolizaPago
{
    public int Id { get; set; }
    public int CuotaId { get; set; }
    public decimal Monto { get; set; }
    public DateTime FechaPago { get; set; }
    public string MetodoPago { get; set; } = "OTRO";
    public int? DocumentoId { get; set; }
    public string? NumeroRecibo { get; set; }
    public string? ReferenciaBanco { get; set; }
    public string? Observaciones { get; set; }
    public int? RegistradoPorUsuarioId { get; set; }
    public DateTime FechaCreacion { get; set; }
    public bool Activo { get; set; } = true;
}

public class RegistrarPagoRequest
{
    public decimal Monto { get; set; }
    public DateTime? FechaPago { get; set; }
    public string? MetodoPago { get; set; }
    public int? DocumentoId { get; set; }
    public string? NumeroRecibo { get; set; }
    public string? ReferenciaBanco { get; set; }
    public string? Observaciones { get; set; }
}
