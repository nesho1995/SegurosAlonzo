namespace ReclamosWhatsApp.Models;

public class ReclamoDocumento
{
    public int Id { get; set; }
    public int ReclamoId { get; set; }
    public string Documento { get; set; } = "";
    public bool Recibido { get; set; }
    public DateTime? FechaRecibido { get; set; }
    public int CantidadRequerida { get; set; } = 1;
    public int MinimoAceptable { get; set; } = 1;
    public bool PermiteExcepcion { get; set; }
    public bool ExcepcionAceptada { get; set; }
    public string? ExcepcionObservacion { get; set; }
    public int AdjuntosRecibidos { get; set; }
}
