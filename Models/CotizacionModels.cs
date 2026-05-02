namespace ReclamosWhatsApp.Models;

// ─── Cotización principal ────────────────────────────────────────────────────

public class Cotizacion
{
    public int Id { get; set; }
    public int? ClienteId { get; set; }
    public string ClienteNombre { get; set; } = "";
    public string Ramo { get; set; } = "";
    public DateTime? FechaInicio { get; set; }
    /// <summary>BORRADOR | REVISION | ENVIADA | ACEPTADA | RECHAZADA</summary>
    public string Estado { get; set; } = "BORRADOR";
    public string? Notas { get; set; }
    public string? CreadoPor { get; set; }
    public DateTime FechaCreacion { get; set; }
    public bool Activo { get; set; } = true;
}

// ─── Item / opción por aseguradora ──────────────────────────────────────────

public class CotizacionItem
{
    public int Id { get; set; }
    public int CotizacionId { get; set; }
    public string Aseguradora { get; set; } = "";
    public string? Plan { get; set; }
    public decimal? PrimaAnual { get; set; }
    public decimal? PrimaMensual { get; set; }
    /// <summary>MENSUAL | TRIMESTRAL | SEMESTRAL | ANUAL</summary>
    public string FrecuenciaPago { get; set; } = "MENSUAL";
    public decimal? SumaAsegurada { get; set; }
    public decimal? Deducible { get; set; }
    public int? VigenciaMeses { get; set; }
    public string? Notas { get; set; }
    public decimal? RankingPuntos { get; set; }
    public int? RankingPosicion { get; set; }
    public bool Recomendado { get; set; }
    public bool Activo { get; set; } = true;
    public IEnumerable<CotizacionCobertura> Coberturas { get; set; } = Enumerable.Empty<CotizacionCobertura>();
    public IEnumerable<CotizacionExclusion> Exclusiones { get; set; } = Enumerable.Empty<CotizacionExclusion>();
    public IEnumerable<CotizacionArchivo> Archivos { get; set; } = Enumerable.Empty<CotizacionArchivo>();
}

// ─── Coberturas ──────────────────────────────────────────────────────────────

public class CotizacionCobertura
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Limite { get; set; }
    public bool Aplica { get; set; } = true;
}

// ─── Exclusiones ─────────────────────────────────────────────────────────────

public class CotizacionExclusion
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string Descripcion { get; set; } = "";
}

// ─── Archivos adjuntos ───────────────────────────────────────────────────────

public class CotizacionArchivo
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string NombreArchivo { get; set; } = "";
    public string RutaArchivo { get; set; } = "";
    public string? TipoMime { get; set; }
    public bool Extraido { get; set; }
    public DateTime FechaSubida { get; set; }
}

// ─── Análisis / recomendación ────────────────────────────────────────────────

public class CotizacionAnalisis
{
    public int Id { get; set; }
    public int CotizacionId { get; set; }
    public string? AnalisisTexto { get; set; }
    public string? VentajasJson { get; set; }
    public string? DesventajasJson { get; set; }
    public string? Recomendacion { get; set; }
    public string? CreadoPor { get; set; }
    public DateTime FechaCreacion { get; set; }
}

// ─── Detalle completo (respuesta de GET /{id}) ───────────────────────────────

public class CotizacionDetalle
{
    public Cotizacion Cotizacion { get; set; } = new();
    public IEnumerable<CotizacionItem> Items { get; set; } = Enumerable.Empty<CotizacionItem>();
    public CotizacionAnalisis? Analisis { get; set; }
    public string? ClienteTelefono { get; set; }
}

// ─── Resumen para listado ────────────────────────────────────────────────────

public class CotizacionResumen
{
    public int Id { get; set; }
    public int? ClienteId { get; set; }
    public string ClienteNombre { get; set; } = "";
    public string Ramo { get; set; } = "";
    public DateTime? FechaInicio { get; set; }
    public string Estado { get; set; } = "BORRADOR";
    public string? Notas { get; set; }
    public int TotalItems { get; set; }
    public decimal? MejorPrima { get; set; }
    public string? CreadoPor { get; set; }
    public DateTime FechaCreacion { get; set; }
}
