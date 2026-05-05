using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;
using ReclamosWhatsApp.Security;

namespace ReclamosWhatsApp.Controllers.Api;

[ApiController]
[Authorize(Policy = Permissions.DashboardVer)]
[Route("api/dashboard")]
public class DashboardApiController : ControllerBase
{
    private readonly ReclamoRepository _reclamos;
    private readonly CarteraRepository _cartera;
    private readonly RecordatorioRepository _recordatorios;
    private readonly PagoRepository _pagos;
    private readonly TallerRepository _talleres;
    private readonly AutomationRepository _automatizaciones;
    private readonly GastoRepository _gastos;

    public DashboardApiController(
        ReclamoRepository reclamos,
        CarteraRepository cartera,
        RecordatorioRepository recordatorios,
        PagoRepository pagos,
        TallerRepository talleres,
        AutomationRepository automatizaciones,
        GastoRepository gastos)
    {
        _reclamos = reclamos;
        _cartera = cartera;
        _recordatorios = recordatorios;
        _pagos = pagos;
        _talleres = talleres;
        _automatizaciones = automatizaciones;
        _gastos = gastos;
    }

    [HttpGet]
    public async Task<IActionResult> Get(DateTime? desde = null, DateTime? hasta = null, string? aseguradora = null, string? ciudad = null)
    {
        try
        {
            dynamic reclamos = await _reclamos.GetDashboardStatsAsync();
            dynamic cartera = await _cartera.GetDashboardStatsAsync(aseguradora, ciudad, desde, hasta);
            dynamic recordatorios = await _recordatorios.GetStatsAsync();
            dynamic pagos = await _pagos.GetStatsAsync(aseguradora, ciudad, desde, hasta);

            var model = new DashboardViewModel
            {
                TotalClientes = Convert.ToInt32(cartera.TotalClientes),
                ClientesActivos = Convert.ToInt32(cartera.ClientesActivos),
                TotalPolizas = Convert.ToInt32(cartera.TotalPolizas),
                PolizasActivas = Convert.ToInt32(cartera.PolizasActivas),
                PolizasPorVencer30 = Convert.ToInt32(cartera.PolizasPorVencer30),
                PolizasPorVencer15 = Convert.ToInt32(cartera.PolizasPorVencer15),
                PolizasPorVencer7 = Convert.ToInt32(cartera.PolizasPorVencer7),
                PolizasVencidas = Convert.ToInt32(cartera.PolizasVencidas),
                PagosPendientes = Convert.ToInt32(cartera.PagosPendientes),
                PrimaTotalActiva = Convert.ToDecimal(cartera.PrimaTotalActiva),
                ReclamosTotal = Convert.ToInt32(reclamos.Total),
                ReclamosPendientes = Convert.ToInt32(reclamos.Pendientes),
                ReclamosCompletos = Convert.ToInt32(reclamos.Completos),
                ReclamosErrores = Convert.ToInt32(reclamos.Errores),
                ReclamosConDocumentosPendientes = Convert.ToInt32(reclamos.ConDocumentosPendientes),
                ReclamosCerradosMes = Convert.ToInt32(reclamos.CerradosMes),
                MontoEstimadoReclamos = Convert.ToDecimal(reclamos.MontoEstimado),
                MontoAprobadoReclamos = Convert.ToDecimal(reclamos.MontoAprobado),
                MontoPagadoReclamos = Convert.ToDecimal(reclamos.MontoPagado),
                RecordatoriosPendientes = Convert.ToInt32(recordatorios.Pendientes),
                RecordatoriosErrores = Convert.ToInt32(recordatorios.Errores),
                AutomatizacionesErrores = await _automatizaciones.CountErroresAsync(desde, hasta),
                GastosMes = await _gastos.GetTotalMesAsync(),
                DatosPendientesRevision = await _cartera.CountDatosPendientesRevisionAsync(),
                ProximasRenovaciones = await _cartera.GetProximasRenovacionesAsync(45, 8, aseguradora, ciudad)
            };

            return Ok(new
            {
                model,
                pagos = new
                {
                    pendientes = Convert.ToInt32(pagos.Pendientes),
                    vencidas = Convert.ToInt32(pagos.Vencidas),
                    pagadas = Convert.ToInt32(pagos.Pagadas),
                    montoPendiente = Convert.ToDecimal(pagos.MontoPendiente),
                    montoVencido = Convert.ToDecimal(pagos.MontoVencido)
                },
                talleres = new
                {
                    detectadosPendientes = await _talleres.CountDetectadosPendientesAsync()
                },
                filtros = new
                {
                    aseguradoras = await _cartera.GetAseguradorasAsync(),
                    ciudades = await _cartera.GetCiudadesAsync()
                }
            });
        }
        catch (Exception ex)
        {
            // Log completo en servidor, nunca exponer detalle al cliente
            _ = ex; // el logger global de Program.cs ya captura la excepcion no manejada
            return StatusCode(500, new { error = "Error al cargar el dashboard. Intente de nuevo." });
        }
    }

    [HttpGet("graficos")]
    public async Task<IActionResult> Graficos()
    {
        var primaMensual       = await _cartera.GetPrimaMensualAsync(12);
        var porAseguradora     = await _cartera.GetDistribucionAseguradorasAsync();
        var porEstado          = await _cartera.GetDistribucionEstadosAsync();
        var cuotasMensuales    = await _cartera.GetCuotasMensualesAsync(6);

        return Ok(new { primaMensual, porAseguradora, porEstado, cuotasMensuales });
    }

    [HttpGet("tareas")]
    public async Task<IActionResult> TareasHoy()
    {
        var tareas = await _cartera.GetTareasHoyAsync();
        return Ok(tareas);
    }
}
