namespace ReclamosWhatsApp.Models;

public class DashboardViewModel
{
    public int TotalClientes { get; set; }
    public int ClientesActivos { get; set; }
    public int TotalPolizas { get; set; }
    public int PolizasActivas { get; set; }
    public int PolizasPorVencer30 { get; set; }
    public int PolizasPorVencer15 { get; set; }
    public int PolizasPorVencer7 { get; set; }
    public int PolizasVencidas { get; set; }
    public int PagosPendientes { get; set; }
    public decimal PrimaTotalActiva { get; set; }
    public int ReclamosTotal { get; set; }
    public int ReclamosPendientes { get; set; }
    public int ReclamosCompletos { get; set; }
    public int ReclamosErrores { get; set; }
    public int ReclamosConDocumentosPendientes { get; set; }
    public int ReclamosCerradosMes { get; set; }
    public decimal MontoEstimadoReclamos { get; set; }
    public decimal MontoAprobadoReclamos { get; set; }
    public decimal MontoPagadoReclamos { get; set; }
    public int RecordatoriosPendientes { get; set; }
    public int RecordatoriosErrores { get; set; }
    public int AutomatizacionesErrores { get; set; }
    public decimal GastosMes { get; set; }
    public int DatosPendientesRevision { get; set; }
    public IEnumerable<PolizaResumenDashboard> ProximasRenovaciones { get; set; } = [];
}

public class PolizaResumenDashboard
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public string Cliente { get; set; } = "";
    public string? Aseguradora { get; set; }
    public string? NumeroPoliza { get; set; }
    public string? Ramo { get; set; }
    public DateTime? Hasta { get; set; }
    public decimal? PrimaTotal { get; set; }
    public int DiasRestantes { get; set; }
    public string EstadoPago { get; set; } = "";
}
