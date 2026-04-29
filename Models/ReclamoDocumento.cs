namespace ReclamosWhatsApp.Models;

public class ReclamoDocumento
{
    public int Id { get; set; }
    public int ReclamoId { get; set; }
    public string Documento { get; set; } = "";
    public bool Recibido { get; set; }
    public DateTime? FechaRecibido { get; set; }
}