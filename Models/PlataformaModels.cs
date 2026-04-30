namespace ReclamosWhatsApp.Models;

public class EmpresaConfiguracion
{
    public int Id { get; set; }
    public string NombreEmpresa { get; set; } = "Seguros Alonzo";
    public string? TelefonoEmpresa { get; set; }
    public string? LogoRuta { get; set; }
    public string? LogoUrl { get; set; }
    public string? ColorPrimario { get; set; }
    public DateTime FechaActualizacion { get; set; }
    public int? UsuarioActualizacionId { get; set; }
}

public class Gasto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Today;
    public string Categoria { get; set; } = "Otros";
    public string Descripcion { get; set; } = "";
    public string? Proveedor { get; set; }
    public decimal Monto { get; set; }
    public string Moneda { get; set; } = "HNL";
    public string? MetodoPago { get; set; }
    public string? Referencia { get; set; }
    public int? DocumentoId { get; set; }
    public string Estado { get; set; } = "REGISTRADO";
    public int? CreadoPor { get; set; }
    public DateTime FechaCreacion { get; set; }
    public bool Activo { get; set; } = true;
}

public class GastoResumen
{
    public decimal TotalMes { get; set; }
    public IEnumerable<CategoriaTotal> PorCategoria { get; set; } = [];
}

public class CategoriaTotal
{
    public string Categoria { get; set; } = "";
    public decimal Total { get; set; }
}
