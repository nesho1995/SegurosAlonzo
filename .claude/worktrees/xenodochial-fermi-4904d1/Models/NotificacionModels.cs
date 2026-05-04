namespace ReclamosWhatsApp.Models;

public class NotificacionInterna
{
    public int Id { get; set; }
    public int? UsuarioId { get; set; }
    public string Tipo { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Mensaje { get; set; } = "";
    public string EntidadTipo { get; set; } = "";
    public int? EntidadId { get; set; }
    public string Referencia { get; set; } = "";
    public bool Leida { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaLectura { get; set; }
}
