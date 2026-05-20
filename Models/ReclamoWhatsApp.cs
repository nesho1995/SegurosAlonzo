namespace ReclamosWhatsApp.Models;

public class ReclamoWhatsApp
{
    public int Id { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string? Asunto { get; set; }
    public string? Aseguradora { get; set; }
    public string? Asegurado { get; set; }
    public string? Poliza { get; set; }
    public string? Placa { get; set; }
    public string? Reclamo { get; set; }
    public string? Conductor { get; set; }
    public string? Celular { get; set; }
    public DateTime? FechaNotificacion { get; set; }
    public string? LugarAccidente { get; set; }
    public string? MensajeWhatsApp { get; set; }
    public string Estado { get; set; } = "PENDIENTE";
    public int? ClienteId { get; set; }
    public int? PolizaId { get; set; }
    public string? NumeroReclamo { get; set; }
    public DateTime? FechaReclamo { get; set; }
    public string? TipoReclamo { get; set; }
    public string? EstadoReclamo { get; set; }
    public int? TallerSugeridoId { get; set; }
    public int? TallerAsignadoId { get; set; }
    public string? CiudadDetectada { get; set; }
    public string? MotivoSugerenciaTaller { get; set; }
    public string? Descripcion { get; set; }
    public decimal? MontoEstimado { get; set; }
    public decimal? MontoAprobado { get; set; }
    public decimal? MontoPagado { get; set; }
    public string? CorreoAseguradoraPrincipal { get; set; }
    public string? CorreoAseguradoraCopia { get; set; }
    public string? RespuestaAseguradora { get; set; }
    public DateTime? FechaRespuestaAseguradora { get; set; }
    public bool AseguradoraAprobado { get; set; }
    public decimal? MontoDeducible { get; set; }
    public decimal? MontoRsa { get; set; }
    public string MonedaPagosFinales { get; set; } = "LPS";
    public string EstadoDeducible { get; set; } = "NO_APLICA";
    public string EstadoRsa { get; set; } = "NO_APLICA";
    public DateTime? FechaSolicitudDeducible { get; set; }
    public DateTime? FechaSolicitudRsa { get; set; }
    public string EstadoCotizaciones { get; set; } = "PENDIENTE_VISITA_TALLERES";
    public string? CotizacionesNota { get; set; }
    public bool CasoEspecial { get; set; }
    public string? CasoEspecialNota { get; set; }
    public string EstadoSeguimiento { get; set; } = "NO_REVISADO";
    public DateTime? FechaUltimaRevision { get; set; }
    public int? UsuarioUltimaRevisionId { get; set; }
    public DateTime? ActualizadoEn { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaEnvio { get; set; }
    public string? Error { get; set; }
}
