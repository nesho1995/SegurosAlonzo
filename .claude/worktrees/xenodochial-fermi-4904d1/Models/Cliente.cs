namespace ReclamosWhatsApp.Models;

public class Cliente
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Telefono { get; set; }
    public string? TelefonoSecundario { get; set; }
    public string? TelefonosExtraJson { get; set; }
    public string? Contacto { get; set; }
    public string? Email { get; set; }
    public string? CorreosExtraJson { get; set; }
    public string? Identidad { get; set; }
    public DateTime? FechaNacimiento { get; set; }
    public string? Ciudad { get; set; }
    public string? Observaciones { get; set; }
    public string? ReferidoPorNombre { get; set; }
    public bool ReferidoDetectado { get; set; }
    public bool RequiereRevisionManual { get; set; }
    public string EstadoRevision { get; set; } = "OK";
    public string? MotivoRevision { get; set; }
    public string? NotasCalidadJson { get; set; }
    public bool DatosRevisados { get; set; }
    public DateTime? FechaRevision { get; set; }
    public int? UsuarioRevisionId { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
}
