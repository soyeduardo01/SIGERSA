using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SIGERSA.Application.Notifications;
using SIGERSA.Domain.Notifications;

namespace SIGERSA.Tests.Application;

public sealed class InspectionReminderPushDispatcherTests
{
    [Fact]
    public async Task DueReminderIsSentAndMarkedExactlyOnce()
    {
        var userId = Guid.NewGuid();
        var reminder = Reminder(userId, "RECORDATORIO_INSPECCION_7_DIAS");
        var dispatchRepository = new FakeDispatchRepository(reminder);
        var subscriptionRepository = new FakeSubscriptionRepository(userId);
        var sender = new FakeSender();
        var dispatcher = CreateDispatcher(dispatchRepository, subscriptionRepository, sender);

        var result = await dispatcher.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(new InspectionReminderPushRunResult(1, 1, 0), result);
        Assert.Equal(reminder.NotificationId, dispatchRepository.SentNotificationId);
        Assert.Null(dispatchRepository.FailedNotificationId);
        Assert.NotNull(sender.Message);
        Assert.Equal("SIGERSA · Recordatorio de inspección", sender.Message.Title);
        Assert.Equal("/modulo.html?module=programacion", sender.Message.Url);
        Assert.True(sender.Message.RequireInteraction);
        Assert.Contains(reminder.EvaluationNumber, sender.Message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReminderWithoutActiveSubscriptionIsScheduledForRetry()
    {
        var reminder = Reminder(Guid.NewGuid(), "RECORDATORIO_INSPECCION_30_DIAS");
        var dispatchRepository = new FakeDispatchRepository(reminder);
        var dispatcher = CreateDispatcher(
            dispatchRepository,
            new FakeSubscriptionRepository(null),
            new FakeSender());

        var result = await dispatcher.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(new InspectionReminderPushRunResult(1, 0, 1), result);
        Assert.Null(dispatchRepository.SentNotificationId);
        Assert.Equal(reminder.NotificationId, dispatchRepository.FailedNotificationId);
        Assert.Equal(TimeSpan.FromMinutes(15), dispatchRepository.RetryDelay);
        Assert.Equal(8, dispatchRepository.MaximumAttempts);
    }

    private static InspectionReminderPushDispatcher CreateDispatcher(
        IInspectionReminderPushRepository dispatchRepository,
        IWebPushSubscriptionRepository subscriptionRepository,
        IWebPushSender sender)
    {
        var webPush = new WebPushNotificationService(subscriptionRepository, sender);
        return new InspectionReminderPushDispatcher(
            dispatchRepository,
            webPush,
            Options.Create(new InspectionReminderPushOptions()),
            NullLogger<InspectionReminderPushDispatcher>.Instance);
    }

    private static InspectionReminderPushDispatch Reminder(Guid userId, string type) => new(
        Guid.NewGuid(), userId, type, "La próxima inspección es en 7 días.", Guid.NewGuid(),
        "EVA-2026-001", "Planta de prueba", DateTimeOffset.UtcNow, 1);

    private sealed class FakeDispatchRepository(InspectionReminderPushDispatch reminder)
        : IInspectionReminderPushRepository
    {
        public Guid? SentNotificationId { get; private set; }
        public Guid? FailedNotificationId { get; private set; }
        public TimeSpan? RetryDelay { get; private set; }
        public int? MaximumAttempts { get; private set; }

        public Task<IReadOnlyList<InspectionReminderPushDispatch>> ClaimDueAsync(
            int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InspectionReminderPushDispatch>>([reminder]);

        public Task MarkSentAsync(Guid notificationId, CancellationToken cancellationToken = default)
        {
            SentNotificationId = notificationId;
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid notificationId,
            string errorMessage,
            TimeSpan retryDelay,
            int maximumAttempts,
            CancellationToken cancellationToken = default)
        {
            FailedNotificationId = notificationId;
            RetryDelay = retryDelay;
            MaximumAttempts = maximumAttempts;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSubscriptionRepository(Guid? subscribedUserId)
        : IWebPushSubscriptionRepository
    {
        public Task UpsertAsync(Guid userId, WebPushSubscriptionDraft subscription, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> RemoveAsync(Guid userId, string endpoint, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<IReadOnlyList<WebPushSubscriptionData>> GetActiveForUserAsync(
            Guid userId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<WebPushSubscriptionData> result = subscribedUserId == userId
                ? [new WebPushSubscriptionData(Guid.NewGuid(), userId, "https://fcm.googleapis.com/fcm/send/test", "p256dh", "auth", null)]
                : [];
            return Task.FromResult(result);
        }

        public Task MarkSentAsync(Guid subscriptionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkFailedAsync(Guid subscriptionId, bool deactivate, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSender : IWebPushSender
    {
        public bool Enabled => true;
        public string PublicKey => "public";
        public WebPushMessage? Message { get; private set; }
        public bool IsAllowedEndpoint(Uri endpoint) => true;

        public Task SendAsync(WebPushSubscriptionData subscription, WebPushMessage message, CancellationToken cancellationToken = default)
        {
            Message = message;
            return Task.CompletedTask;
        }
    }
}
