using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SIGERSA.Domain.Notifications;
using SIGERSA.Infrastructure.Configuration;
using WebPush;

namespace SIGERSA.Infrastructure.Notifications;

public sealed class WebPushSender(HttpClient httpClient, IOptions<WebPushOptions> options) : IWebPushSender, IDisposable
{
    private readonly WebPushOptions _options = options.Value;
    private readonly WebPushClient _client = new(httpClient);

    public bool Enabled => _options.Enabled;
    public string PublicKey => _options.PublicKey;

    public bool IsAllowedEndpoint(Uri endpoint) =>
        _options.AllowedEndpointHosts.Any(allowed =>
        {
            var normalized = allowed.Trim().ToLowerInvariant();
            return normalized.StartsWith('.')
                ? endpoint.Host.EndsWith(normalized, StringComparison.OrdinalIgnoreCase)
                : endpoint.Host.Equals(normalized, StringComparison.OrdinalIgnoreCase);
        });

    public async Task SendAsync(
        WebPushSubscriptionData subscription,
        WebPushMessage message,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            title = message.Title,
            body = message.Body,
            url = message.Url,
            tag = message.Tag,
            requireInteraction = message.RequireInteraction,
            icon = "/pwa-192x192.png",
            badge = "/pwa-64x64.png"
        });
        var target = new PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth);
        var vapid = new VapidDetails(_options.Subject, _options.PublicKey, _options.PrivateKey);
        var sendOptions = new Dictionary<string, object>
        {
            ["vapidDetails"] = vapid,
            ["TTL"] = _options.TimeToLiveSeconds,
            ["headers"] = new Dictionary<string, object> { ["Urgency"] = "high" }
        };

        try
        {
            await _client.SendNotificationAsync(target, payload, sendOptions, cancellationToken);
        }
        catch (WebPushException exception)
        {
            var expired = exception.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone;
            throw new WebPushDeliveryException("El proveedor push rechazó la entrega.", expired, exception);
        }
        catch (HttpRequestException exception)
        {
            throw new WebPushDeliveryException("No se pudo contactar al proveedor push.", false, exception);
        }
    }

    public void Dispose() => _client.Dispose();
}
