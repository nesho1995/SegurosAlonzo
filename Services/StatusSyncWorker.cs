using ReclamosWhatsApp.Data;

namespace ReclamosWhatsApp.Services;

/// <summary>
/// Worker que sincroniza estados de pólizas cada 4 horas.
/// Marca como VENCIDA cualquier póliza cuya fecha "hasta" ya pasó
/// y cuyo estado almacenado no refleja esa realidad.
/// </summary>
public class StatusSyncWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(4);

    private readonly IServiceProvider _services;
    private readonly ILogger<StatusSyncWorker> _logger;

    public StatusSyncWorker(IServiceProvider services, ILogger<StatusSyncWorker> logger)
    {
        _services = services;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Primera ejecución: esperar 5 minutos después del arranque
        // para no solapar con la sincronización inicial del startup.
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope      = _services.CreateScope();
                var cartera          = scope.ServiceProvider.GetRequiredService<CarteraRepository>();
                var polizasActualizadas = await cartera.SincronizarEstadosPolizasAsync();
                var clientesActualizados = await cartera.SincronizarEstadosClientesAsync();

                if (polizasActualizadas > 0)
                    _logger.LogInformation("StatusSyncWorker: {Count} pólizas actualizadas a VENCIDA.", polizasActualizadas);
                if (clientesActualizados > 0)
                    _logger.LogInformation("StatusSyncWorker: {Count} clientes con estado_negocio actualizado.", clientesActualizados);
                if (polizasActualizadas == 0 && clientesActualizados == 0)
                    _logger.LogDebug("StatusSyncWorker: todos los estados están sincronizados.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StatusSyncWorker: error al sincronizar estados de pólizas.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
