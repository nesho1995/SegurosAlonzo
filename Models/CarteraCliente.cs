namespace ReclamosWhatsApp.Models
{
    public class CarteraCliente
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? CompaniaSeguros { get; set; }
        public string? Ramo { get; set; }
        public int? Cuotas { get; set; }
        public string? FormaPago { get; set; }
        public string? Poliza { get; set; }
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
        public string? SumaAsegurada { get; set; }
        public DateTime? Vigencia { get; set; }
        public DateTime? Hasta { get; set; }
        public string? Medio { get; set; }
        public string? Vehiculo { get; set; }
        public string? Contacto { get; set; }
        public string? Correo { get; set; }
        public string? Observaciones { get; set; }
        public DateTime? Cumpleanos { get; set; }
        public string? EmisionRenovacion { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
