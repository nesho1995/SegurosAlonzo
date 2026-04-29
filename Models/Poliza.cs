namespace ReclamosWhatsApp.Models;

public class Poliza
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public int? ClienteContratanteId { get; set; }
    public string? ClienteContratanteNombre { get; set; }
    public int? VehiculoId { get; set; }
    public string? Aseguradora { get; set; }
    public string? Ramo { get; set; }
    public string? RamoNormalizado { get; set; }
    public string? ExtrasJson { get; set; }
    public int? Cuotas { get; set; }
    public string? FormaPago { get; set; }
    public string? NumeroPoliza { get; set; }
    public string? NumeroItem { get; set; }
    public string? Certificado { get; set; }
    public string? Endoso { get; set; }
    public decimal? PrimaNeta { get; set; }
    public decimal? SeguroAsiento { get; set; }
    public decimal? PrimaComercial { get; set; }
    public decimal? Impuesto { get; set; }
    public decimal? GastosEmision { get; set; }
    public decimal? Bomberos { get; set; }
    public decimal? PrimaTotal { get; set; }
    public string? Plan { get; set; }
    public decimal? SumaAsegurada { get; set; }
    public string? SumaAseguradaTextoOriginal { get; set; }
    public decimal? MaximoVitalicio { get; set; }
    public decimal? SumaAseguradaVida { get; set; }
    public string? MesInicioPoliza { get; set; }
    public DateTime? Vigencia { get; set; }
    public DateTime? Hasta { get; set; }
    public string? Medio { get; set; }
    public string? Vehiculo { get; set; }
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public int? Anio { get; set; }
    public string? Color { get; set; }
    public string? TipoVehiculo { get; set; }
    public string? Placa { get; set; }
    public string? Motor { get; set; }
    public string? VinSerie { get; set; }
    public string? Chasis { get; set; }
    public string? AgenteAsignado { get; set; }
    public string? EmisionRenovacion { get; set; }
    public string? Observacion2 { get; set; }
    public string? TipoProceso { get; set; }
    public string? EstadoPolizaReal { get; set; }
    public string? MotivoCancelacion { get; set; }
    public string? OrigenRamoNormalizado { get; set; }
    public string? OrigenTipoProceso { get; set; }
    public string? OrigenEstadoPolizaReal { get; set; }
    public string? OrigenEstadoPago { get; set; }
    public string? OrigenSumaAsegurada { get; set; }
    public string? MotivoEstadoPago { get; set; }
    public string EstadoPago { get; set; } = "SIN_VALIDAR";
    public string? Observaciones { get; set; }
    public string? ObservacionOriginal { get; set; }
    public string? ObservacionTipo { get; set; }
    public string? PersonaRelacionada { get; set; }
    public string? NotaAdministrativa { get; set; }
    public bool RequiereRevisionManual { get; set; }
    public string EstadoRevision { get; set; } = "OK";
    public string? MotivoRevision { get; set; }
    public string? NotasCalidadJson { get; set; }
    public bool DatosRevisados { get; set; }
    public DateTime? FechaRevision { get; set; }
    public int? UsuarioRevisionId { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
}
