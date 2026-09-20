using Microsoft.Extensions.Options;
using SIGERSA.Application.Notifications;

namespace SIGERSA.Worker;

public partial class Worker(
    IServiceScopeFactory scopeFactory,
    IOptions<InspectionReminderPushOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<InspectionReminderPushDispatcher>();
                var result = await dispatcher.ProcessDueAsync(stoppingToken);
                if (result.Claimed > 0)
                    LogBatch(logger, result.Claimed, result.Sent, result.Retrying);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogWorkerFailure(logger, exception);
            }

            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds), stoppingToken);
        }
    }

    [LoggerMessage(2000, LogLevel.Information,
        "Lote de recordatorios Web Push: {Claimed} reclamados, {Sent} enviados y {Retrying} pendientes de reintento.")]
    private static partial void LogBatch(ILogger logger, int claimed, int sent, int retrying);

    [LoggerMessage(2001, LogLevel.Error,
        "Falló el ciclo del despachador de recordatorios Web Push.")]
    private static partial void LogWorkerFailure(ILogger logger, Exception exception);
}
