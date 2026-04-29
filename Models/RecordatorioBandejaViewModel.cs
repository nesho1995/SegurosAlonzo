namespace ReclamosWhatsApp.Models;

public class RecordatorioBandejaViewModel
{
    public IEnumerable<Recordatorio> Items { get; set; } = [];
    public RecordatorioFiltro Filtro { get; set; } = new();
    public int Total { get; set; }
    public int TotalPaginas { get; set; }
    public dynamic? Stats { get; set; }
    public IEnumerable<RecordatorioTipoResumen> Tipos { get; set; } = [];
}

public class RecordatorioFiltro
{
    public string? Estado { get; set; } = "PENDIENTE";
    public string? Tipo { get; set; }
    public string? Buscar { get; set; }
    public int Pagina { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class RecordatorioTipoResumen
{
    public string Tipo { get; set; } = "";
    public int Total { get; set; }
}
