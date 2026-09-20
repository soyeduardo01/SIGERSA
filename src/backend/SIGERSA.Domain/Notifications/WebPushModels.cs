namespace SIGERSA.Domain.Notifications;

public sealed record WebPushSubscriptionData(
    Guid Id,
    Guid UserId,
    string Endpoint,
    string P256dh,
    string Auth,
    DateTimeOffset? ExpiresAt);

public sealed record WebPushSubscriptionDraft(
    string Endpoint,
    string P256dh,
    string Auth,
    DateTimeOffset? ExpiresAt,
    string? UserAgent);

public sealed record WebPushMessage(
    string Title,
    string Body,
    string Url,
    string Tag,
    bool RequireInteraction);

public sealed record WebPushDeliveryResult(int Subscriptions, int Sent, int Expired, int Failed);

public sealed class WebPushDeliveryException(string message, bool subscriptionExpired, Exception? innerException = null)
    : Exception(message, innerException)
{
    public bool SubscriptionExpired { get; } = subscriptionExpired;
}
