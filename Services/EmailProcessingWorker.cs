using ReclamosWhatsApp.Data;
using ReclamosWhatsApp.Models;

namespace ReclamosWhatsApp.Services;

public class EmailProcessingWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<EmailProcessingWorker> _logger;

    public EmailProcessingWorker(
        IServiceProvider services,
        IConfiguration config,
        ILogger<EmailProcessingWorker> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = _config.GetValue<int?>("Worker:IntervalSeconds") ?? 30;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var bootScope = _services.CreateScope();
                var bootSettings = bootScope.ServiceProvider.GetRequiredService<AppSettingsRepository>();
                var bootRuntime = await bootSettings.GetReclamoCorreoConfigAsync(_config);
                if (!bootRuntime.WorkerEnabled)
                {
                    _logger.LogWarning("Worker deshabilitado. Activa Worker:Enabled=true cuando ya tengas configurado el correo.");
                    await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
                    continue;
                }

                using var scope = _services.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<ReclamoCorreoProcessingService>();
                var settings = scope.ServiceProvider.GetRequiredService<AppSettingsRepository>();
                var estado = await processor.ProcessAsync();
                await settings.SaveReclamoWorkerEstadoAsync(estado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general procesando correos y recordatorios. Se reintentara en {IntervalSeconds}s.", interval);
            }

            await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
        }
    }

}
