namespace SIGERSA.Domain.Notifications;

public sealed record InspectionReminderPushDispatch(
    Guid NotificationId,
    Guid UserId,
    string Type,
    string Message,
    Guid EvaluationId,
    string EvaluationNumber,
    string EstablishmentName,
    DateTimeOffset ScheduledFor,
    int Attempt);

public interface IInspectionReminderPushRepository
{
    Task<IReadOnlyList<InspectionReminderPushDispatch>> ClaimDueAsync(
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task MarkSentAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid notificationId,
        string errorMessage,
        TimeSpan retryDelay,
        int maximumAttempts,
        CancellationToken cancellationToken = default);
}
