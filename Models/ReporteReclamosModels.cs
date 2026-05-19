namespace ReclamosWhatsApp.Models;

public class ReporteReclamosFiltro
{
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public string? Estado { get; set; }
    public string? Ciudad { get; set; }
    public string? Buscar { get; set; }
    public bool SoloConMovimiento { get; set; }
    public int PageSize { get; set; } = 200;
}

public class ReporteReclamoItem
{
    public int Id { get; set; }
    public string? Reclamo { get; set; }
    public string? Poliza { get; set; }
    public string? Placa { get; set; }
    public string? Conductor { get; set; }
    public string? Celular { get; set; }
    public string? Asegurado { get; set; }
    public string Estado { get; set; } = "";
    public string? CiudadDetectada { get; set; }
    public string? Descripcion { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaUltimoRecordatorio { get; set; }
    public int CantidadRecordatorios { get; set; }
    public int DocumentosPendientes { get; set; }
    public int DocumentosRecibidos { get; set; }
    public int EventosPeriodo { get; set; }
    public DateTime? UltimoMovimientoFecha { get; set; }
    public string? UltimoMovimientoAccion { get; set; }
    public string? UltimoMovimientoDescripcion { get; set; }
    public string? UltimoMovimientoUsuario { get; set; }
}

public class ReporteReclamosResumen
{
    public int Total { get; set; }
    public int ConPendientes { get; set; }
    public int SinMovimientoPeriodo { get; set; }
    public int ConTelefono { get; set; }
    public int SinTelefono { get; set; }
}
