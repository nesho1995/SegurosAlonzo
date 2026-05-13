namespace ReclamosWhatsApp.Models;

public class ReclamoHistoricoImportPreview
{
    public List<ReclamoHistoricoImportRow> Rows { get; set; } = new();
    public int TotalRows => Rows.Count;
    public int ErrorCount => Rows.Count(x => x.Errors.Count > 0);
    public int WarningCount => Rows.Sum(x => x.Warnings.Count);
    public int ImportableCount => Rows.Count(x => x.Errors.Count == 0 && !x.Duplicado);
    public int DuplicateCount => Rows.Count(x => x.Duplicado);
}

public class ReclamoHistoricoImportRow
{
    public int RowNumber { get; set; }
    public string Conductor { get; set; } = "";
    public string Cliente { get; set; } = "";
    public string Poliza { get; set; } = "";
    public string Reclamo { get; set; } = "";
    public string Vehiculo { get; set; } = "";
    public string Placa { get; set; } = "";
    public string Celular { get; set; } = "";
    public string Observaciones { get; set; } = "";
    public DateTime? FechaNotificacion { get; set; }
    public bool Duplicado { get; set; }
    public Dictionary<string, bool> DocumentosRecibidos { get; set; } = new();
    public List<ImportIssue> Errors { get; set; } = new();
    public List<ImportIssue> Warnings { get; set; } = new();
}

public record ImportIssue(string Field, string Message);

public class ReclamoHistoricoImportResult
{
    public int Importados { get; set; }
    public int Duplicados { get; set; }
    public int Rechazados { get; set; }
}
