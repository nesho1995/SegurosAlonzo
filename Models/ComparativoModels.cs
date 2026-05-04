namespace ReclamosWhatsApp.Models;

public class Comparativo
{
    public int Id { get; set; }
    public string Cliente { get; set; } = "";
    public string? Vehiculo { get; set; }
    public string? Notas { get; set; }
    public string Estado { get; set; } = "borrador"; // borrador | listo
    public DateTime CreadoEn { get; set; }
    public int EmpresaId { get; set; }
    public int UsuarioId { get; set; }
    public List<ComparativoItem> Items { get; set; } = [];
}

public class ComparativoItem
{
    public int Id { get; set; }
    public int ComparativoId { get; set; }
    public string Aseguradora { get; set; } = "";
    public string? NombreArchivo { get; set; }
    public string? TextoExtraido { get; set; }

    // Primas
    public decimal? PrimaAnual { get; set; }
    public decimal? PrimaMensual { get; set; }
    public decimal? PrimaContado { get; set; }          // con descuento pronto pago
    public decimal? DescuentoContado { get; set; }      // % o monto
    public bool DescuentoEsPorcentaje { get; set; }
    public decimal? RecargoFinanciamiento { get; set; } // % o monto
    public bool RecargoEsPorcentaje { get; set; }
    public decimal? PrimaFinanciada { get; set; }       // con recargo aplicado
    public string? FormaPago { get; set; }              // Contado | 3 cuotas | Mensual...

    // Cobertura
    public decimal? SumaAsegurada { get; set; }
    public decimal? DeducibleColision { get; set; }     // puede ser % o monto
    public bool DeducibleColisionEsPorcentaje { get; set; }
    public decimal? DeducibleRobo { get; set; }
    public bool DeducibleRoboEsPorcentaje { get; set; }

    // Vigencia
    public string? VigenciaDesde { get; set; }
    public string? VigenciaHasta { get; set; }

    // Coberturas/exclusiones como texto libre (JSON array)
    public string? CoberturasJson { get; set; }
    public string? ExclusionesJson { get; set; }

    // Ranking
    public decimal? Score { get; set; }
    public int? Posicion { get; set; }

    public DateTime CreadoEn { get; set; }
}

// DTO para el detalle completo
public class ComparativoDetalle
{
    public Comparativo Comparativo { get; set; } = null!;
    public List<ComparativoItemDetalle> Items { get; set; } = [];
}

public class ComparativoItemDetalle : ComparativoItem
{
    public List<string> Coberturas { get; set; } = [];
    public List<string> Exclusiones { get; set; } = [];
    public decimal? AhorroContado { get; set; }  // PrimaFinanciada - PrimaContado
}

public class ComparativoListResponse
{
    public List<Comparativo> Items { get; set; } = [];
    public int Total { get; set; }
}
