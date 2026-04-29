namespace ReclamosWhatsApp.Services;

public class AlertasWorker : BackgroundService
{
    private readonly ILogger<AlertasWorker> _logger;

    public AlertasWorker(ILogger<AlertasWorker> logger)
    {
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AlertasWorker deshabilitado temporalmente.");
        return Task.CompletedTask;
    }
}