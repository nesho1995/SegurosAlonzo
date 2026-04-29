namespace ReclamosWhatsApp.Models;

public class ClienteListado
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Telefono { get; set; }
    public string? TelefonoSecundario { get; set; }
    public string? TelefonosExtraJson { get; set; }
    public string? Contacto { get; set; }
    public string? Email { get; set; }
    public string? CorreosExtraJson { get; set; }
    public string? Ciudad { get; set; }
    public bool Activo { get; set; }
    public bool RequiereRevisionManual { get; set; }
    public string EstadoRevision { get; set; } = "OK";
    public string? MotivoRevision { get; set; }
    public int Polizas { get; set; }
    public int PolizasActivas { get; set; }
    public DateTime FechaCreacion { get; set; }
}

public class ClienteDetalle
{
    public Cliente Cliente { get; set; } = new();
    public IEnumerable<string> Telefonos { get; set; } = Enumerable.Empty<string>();
    public IEnumerable<Poliza> Polizas { get; set; } = Enumerable.Empty<Poliza>();
}
