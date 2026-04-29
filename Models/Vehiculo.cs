namespace ReclamosWhatsApp.Models;

public class Vehiculo
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public int? Anio { get; set; }
    public string? Color { get; set; }
    public string? Tipo { get; set; }
    public string? Placa { get; set; }
    public string? Motor { get; set; }
    public string? VinSerie { get; set; }
    public string? Chasis { get; set; }
    public string? OrigenDatos { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}
