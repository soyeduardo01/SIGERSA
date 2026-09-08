namespace SIGERSA.Worker;

public partial class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            LogWorkerActive(logger, DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Debug,
        Message = "Worker de bandeja de salida activo en {TimestampUtc}")]
    private static partial void LogWorkerActive(ILogger logger, DateTimeOffset timestampUtc);
}
