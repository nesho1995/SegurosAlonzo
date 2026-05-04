namespace ReclamosWhatsApp.Models;

public class ReclamoExpediente
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public int? PolizaId { get; set; }
    public string? NumeroReclamo { get; set; }
    public DateTime FechaReclamo { get; set; }
    public string TipoReclamo { get; set; } = "GENERAL";
    public string EstadoReclamo { get; set; } = "NUEVO";
    public int? TallerSugeridoId { get; set; }
    public int? TallerAsignadoId { get; set; }
    public string? CiudadDetectada { get; set; }
    public string? MotivoSugerenciaTaller { get; set; }
    public string? Descripcion { get; set; }
    public decimal? MontoEstimado { get; set; }
    public decimal? MontoAprobado { get; set; }
    public decimal? MontoPagado { get; set; }
    public string? Observaciones { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime ActualizadoEn { get; set; }
}

public class ReclamoRequisito
{
    public int Id { get; set; }
    public string TipoReclamo { get; set; } = "GENERAL";
    public string TipoDocumento { get; set; } = "OTRO";
    public bool Requerido { get; set; } = true;
    public bool Activo { get; set; } = true;
}
