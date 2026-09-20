using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIGERSA.Domain.Notifications;

namespace SIGERSA.Application.Notifications;

public sealed class InspectionReminderPushOptions
{
    public const string SectionName = "InspectionReminderPush";

    public bool Enabled { get; init; } = true;
    public int PollSeconds { get; init; } = 30;
    public int BatchSize { get; init; } = 50;
    public int LeaseMinutes { get; init; } = 5;
    public int RetryMinutes { get; init; } = 15;
    public int MaximumAttempts { get; init; } = 8;
}

public sealed record InspectionReminderPushRunResult(int Claimed, int Sent, int Retrying);

public sealed partial class InspectionReminderPushDispatcher(
    IInspectionReminderPushRepository repository,
    WebPushNotificationService webPush,
    IOptions<InspectionReminderPushOptions> options,
    ILogger<InspectionReminderPushDispatcher> logger)
{
    public async Task<InspectionReminderPushRunResult> ProcessDueAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || !webPush.GetConfiguration().Enabled)
            return new InspectionReminderPushRunResult(0, 0, 0);

        var reminders = await repository.ClaimDueAsync(
            settings.BatchSize,
            TimeSpan.FromMinutes(settings.LeaseMinutes),
            cancellationToken);
        var sent = 0;
        var retrying = 0;

        foreach (var reminder in reminders)
        {
            try
            {
                var delivery = await webPush.SendToUserAsync(
                    reminder.UserId,
                    new SendWebPushRequest(
                        "SIGERSA · Recordatorio de inspección",
                        $"{reminder.Message} Evaluación {reminder.EvaluationNumber} · {reminder.EstablishmentName}.",
                        "/modulo.html?module=programacion",
                        $"recordatorio-inspeccion-{reminder.NotificationId:N}",
                        RequireInteraction: IsUrgent(reminder.Type)),
                    cancellationToken);

                if (delivery.Sent > 0)
                {
                    await repository.MarkSentAsync(reminder.NotificationId, cancellationToken);
                    sent++;
                    LogSent(logger, reminder.NotificationId, reminder.UserId, delivery.Sent);
                }
                else
                {
                    await RecordFailureAsync(reminder, "No hay una suscripción activa que haya aceptado el mensaje.", cancellationToken);
                    retrying++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await RecordFailureAsync(reminder, exception.Message, cancellationToken);
                retrying++;
                LogFailure(logger, reminder.NotificationId, reminder.UserId, exception);
            }
        }

        return new InspectionReminderPushRunResult(reminders.Count, sent, retrying);
    }

    private Task RecordFailureAsync(
        InspectionReminderPushDispatch reminder,
        string errorMessage,
        CancellationToken cancellationToken) =>
        repository.MarkFailedAsync(
            reminder.NotificationId,
            errorMessage,
            TimeSpan.FromMinutes(options.Value.RetryMinutes),
            options.Value.MaximumAttempts,
            cancellationToken);

    private static bool IsUrgent(string type) =>
        type.EndsWith("_7_DIAS", StringComparison.Ordinal) ||
        type.EndsWith("_HOY", StringComparison.Ordinal);

    [LoggerMessage(1100, LogLevel.Information,
        "Recordatorio Web Push {NotificationId} enviado al usuario {UserId} en {DeviceCount} dispositivo(s).")]
    private static partial void LogSent(ILogger logger, Guid notificationId, Guid userId, int deviceCount);

    [LoggerMessage(1101, LogLevel.Warning,
        "El recordatorio Web Push {NotificationId} para el usuario {UserId} se reintentará.")]
    private static partial void LogFailure(ILogger logger, Guid notificationId, Guid userId, Exception exception);
}
