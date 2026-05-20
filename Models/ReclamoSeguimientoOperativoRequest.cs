namespace ReclamosWhatsApp.Models;

public sealed record ReclamoSeguimientoOperativoRequest(
    decimal? MontoDeducible,
    decimal? MontoRsa,
    string? EstadoDeducible,
    string? EstadoRsa,
    string? EstadoCotizaciones,
    string? CotizacionesNota,
    bool CasoEspecial,
    string? CasoEspecialNota);
