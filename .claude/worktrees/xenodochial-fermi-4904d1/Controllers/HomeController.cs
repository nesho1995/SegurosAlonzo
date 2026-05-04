using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Controllers;

public class HomeController : Controller
{
    private readonly ReclamoRepository _reclamos;
    private readonly CarteraRepository _cartera;
    private readonly RecordatorioRepository _recordatorios;

    public HomeController(ReclamoRepository reclamos, CarteraRepository cartera, RecordatorioRepository recordatorios)
    {
        _reclamos = reclamos;
        _cartera = cartera;
        _recordatorios = recordatorios;
    }

    public async Task<IActionResult> Index()
    {
        dynamic reclamos = await _reclamos.GetDashboardStatsAsync();
        dynamic cartera = await _cartera.GetDashboardStatsAsync();
        dynamic recordatorios = await _recordatorios.GetStatsAsync();

        var model = new DashboardViewModel
        {
            TotalClientes = Convert.ToInt32(cartera.TotalClientes),
            ClientesActivos = Convert.ToInt32(cartera.ClientesActivos),
            TotalPolizas = Convert.ToInt32(cartera.TotalPolizas),
            PolizasActivas = Convert.ToInt32(cartera.PolizasActivas),
            PolizasPorVencer30 = Convert.ToInt32(cartera.PolizasPorVencer30),
            PolizasVencidas = Convert.ToInt32(cartera.PolizasVencidas),
            PagosPendientes = Convert.ToInt32(cartera.PagosPendientes),
            PrimaTotalActiva = Convert.ToDecimal(cartera.PrimaTotalActiva),
            ReclamosTotal = Convert.ToInt32(reclamos.Total),
            ReclamosPendientes = Convert.ToInt32(reclamos.Pendientes),
            ReclamosCompletos = Convert.ToInt32(reclamos.Completos),
            ReclamosErrores = Convert.ToInt32(reclamos.Errores),
            RecordatoriosPendientes = Convert.ToInt32(recordatorios.Pendientes),
            RecordatoriosErrores = Convert.ToInt32(recordatorios.Errores),
            ProximasRenovaciones = await _cartera.GetProximasRenovacionesAsync()
        };

        return View(model);
    }

    public IActionResult Error()
    {
        return View();
    }
}
