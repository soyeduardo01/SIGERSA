using SIGERSA.Application.Notifications;
using SIGERSA.Domain.Notifications;

namespace SIGERSA.Tests.Application;

public sealed class WebPushNotificationServiceTests
{
    [Fact]
    public async Task SubscribeAssociatesSubscriptionWithAuthenticatedUser()
    {
        var repository = new FakeRepository();
        var service = new WebPushNotificationService(repository, new FakeSender());
        var userId = Guid.NewGuid();

        await service.SubscribeAsync(userId, new WebPushSubscriptionRequest(
            "https://fcm.googleapis.com/fcm/send/device-token",
            new WebPushSubscriptionKeys(Base64Url(new byte[65]), Base64Url(new byte[16])),
            null,
            "Test browser"), CancellationToken.None);

        Assert.Equal(userId, repository.UpsertedUserId);
        Assert.Equal("https://fcm.googleapis.com/fcm/send/device-token", repository.Upserted?.Endpoint);
    }

    [Fact]
    public async Task SubscribeRejectsEndpointOutsideAllowList()
    {
        var service = new WebPushNotificationService(new FakeRepository(), new FakeSender());

        await Assert.ThrowsAsync<ArgumentException>(() => service.SubscribeAsync(
            Guid.NewGuid(),
            new WebPushSubscriptionRequest(
                "https://example.com/push",
                new WebPushSubscriptionKeys(Base64Url(new byte[65]), Base64Url(new byte[16])),
                null,
                null),
            CancellationToken.None));
    }

    [Fact]
    public async Task SendDeactivatesExpiredSubscriptionAndContinuesOtherDevices()
    {
        var subscriptions = new[]
        {
            Subscription(Guid.NewGuid(), "expired"),
            Subscription(Guid.NewGuid(), "active")
        };
        var repository = new FakeRepository(subscriptions);
        var sender = new FakeSender(subscription =>
            subscription.Endpoint.EndsWith("expired", StringComparison.Ordinal)
                ? new WebPushDeliveryException("expired", subscriptionExpired: true)
                : null);
        var service = new WebPushNotificationService(repository, sender);

        var result = await service.SendToUserAsync(Guid.NewGuid(), new SendWebPushRequest(
            "Alerta crítica", "Revisión inmediata requerida."), CancellationToken.None);

        Assert.Equal(new WebPushDeliveryResult(2, 1, 1, 0), result);
        Assert.Contains(subscriptions[0].Id, repository.Deactivated);
        Assert.Contains(subscriptions[1].Id, repository.Sent);
    }

    private static WebPushSubscriptionData Subscription(Guid id, string suffix) =>
        new(id, Guid.NewGuid(), $"https://fcm.googleapis.com/fcm/send/{suffix}", "p256dh", "auth", null);

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class FakeSender(Func<WebPushSubscriptionData, WebPushDeliveryException?>? failure = null)
        : IWebPushSender
    {
        public bool Enabled => true;
        public string PublicKey => "public";
        public bool IsAllowedEndpoint(Uri endpoint) => endpoint.Host == "fcm.googleapis.com";

        public Task SendAsync(
            WebPushSubscriptionData subscription,
            WebPushMessage message,
            CancellationToken cancellationToken = default)
        {
            var exception = failure?.Invoke(subscription);
            return exception is null ? Task.CompletedTask : Task.FromException(exception);
        }
    }

    private sealed class FakeRepository(IReadOnlyList<WebPushSubscriptionData>? subscriptions = null)
        : IWebPushSubscriptionRepository
    {
        public Guid? UpsertedUserId { get; private set; }
        public WebPushSubscriptionDraft? Upserted { get; private set; }
        public List<Guid> Sent { get; } = [];
        public List<Guid> Deactivated { get; } = [];

        public Task UpsertAsync(Guid userId, WebPushSubscriptionDraft subscription, CancellationToken cancellationToken = default)
        {
            UpsertedUserId = userId;
            Upserted = subscription;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(Guid userId, string endpoint, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<IReadOnlyList<WebPushSubscriptionData>> GetActiveForUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(subscriptions ?? (IReadOnlyList<WebPushSubscriptionData>)[]);

        public Task MarkSentAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
        {
            Sent.Add(subscriptionId);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(Guid subscriptionId, bool deactivate, CancellationToken cancellationToken = default)
        {
            if (deactivate) Deactivated.Add(subscriptionId);
            return Task.CompletedTask;
        }
    }
}
