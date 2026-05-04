namespace ReclamosWhatsApp.Models;

public class CatalogoItem
{
    public int Id { get; set; }
    public string TipoCatalogo { get; set; } = "";
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public int Orden { get; set; }
    public bool EsDefault { get; set; }
    public bool PendienteRevision { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }
}

public sealed record CatalogoMergeRequest(int SourceId, int TargetId);
