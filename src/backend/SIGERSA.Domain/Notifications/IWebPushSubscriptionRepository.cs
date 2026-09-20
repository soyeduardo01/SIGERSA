namespace SIGERSA.Domain.Notifications;

public interface IWebPushSubscriptionRepository
{
    Task UpsertAsync(Guid userId, WebPushSubscriptionDraft subscription, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(Guid userId, string endpoint, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebPushSubscriptionData>> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task MarkSentAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(Guid subscriptionId, bool deactivate, CancellationToken cancellationToken = default);
}

public interface IWebPushSender
{
    bool Enabled { get; }
    string PublicKey { get; }
    bool IsAllowedEndpoint(Uri endpoint);
    Task SendAsync(WebPushSubscriptionData subscription, WebPushMessage message, CancellationToken cancellationToken = default);
}
