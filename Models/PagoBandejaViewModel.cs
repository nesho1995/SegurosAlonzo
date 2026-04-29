namespace ReclamosWhatsApp.Models;

public class PagoBandejaViewModel
{
    public IEnumerable<PolizaCuota> Cuotas { get; set; } = [];
    public string? Estado { get; set; } = "PENDIENTE";
    public string? Buscar { get; set; }
    public int Pagina { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int Total { get; set; }
    public int TotalPaginas { get; set; }
    public dynamic? Stats { get; set; }
}
