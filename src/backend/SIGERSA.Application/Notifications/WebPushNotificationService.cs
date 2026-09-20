using SIGERSA.Domain.Notifications;

namespace SIGERSA.Application.Notifications;

public sealed record WebPushConfiguration(bool Enabled, string? PublicKey);
public sealed record WebPushSubscriptionRequest(
    string Endpoint,
    WebPushSubscriptionKeys Keys,
    DateTimeOffset? ExpirationTime,
    string? UserAgent);
public sealed record WebPushSubscriptionKeys(string P256dh, string Auth);
public sealed record SendWebPushRequest(
    string Title,
    string Body,
    string? Url = null,
    string? Tag = null,
    bool RequireInteraction = true);

public sealed class WebPushNotificationService(
    IWebPushSubscriptionRepository repository,
    IWebPushSender sender)
{
    public WebPushConfiguration GetConfiguration() =>
        new(sender.Enabled, sender.Enabled ? sender.PublicKey : null);

    public async Task SubscribeAsync(
        Guid userId,
        WebPushSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        if (!sender.Enabled)
            throw new InvalidOperationException("Las notificaciones push no están configuradas en el servidor.");

        var endpoint = ValidateEndpoint(request.Endpoint);
        ValidateKey(request.Keys.P256dh, nameof(request.Keys.P256dh), 65, 256);
        ValidateKey(request.Keys.Auth, nameof(request.Keys.Auth), 16, 128);
        if (request.ExpirationTime is { } expiration && expiration <= DateTimeOffset.UtcNow)
            throw new ArgumentException("La suscripción push ya expiró.");

        await repository.UpsertAsync(userId, new WebPushSubscriptionDraft(
            endpoint.AbsoluteUri,
            request.Keys.P256dh.Trim(),
            request.Keys.Auth.Trim(),
            request.ExpirationTime,
            TrimTo(request.UserAgent, 500)), cancellationToken);
    }

    public Task<bool> UnsubscribeAsync(Guid userId, string endpoint, CancellationToken cancellationToken)
    {
        var validated = ValidateEndpoint(endpoint);
        return repository.RemoveAsync(userId, validated.AbsoluteUri, cancellationToken);
    }

    public async Task<WebPushDeliveryResult> SendToUserAsync(
        Guid userId,
        SendWebPushRequest request,
        CancellationToken cancellationToken)
    {
        if (!sender.Enabled)
            throw new InvalidOperationException("Las notificaciones push no están configuradas en el servidor.");

        var message = ValidateMessage(request);
        var subscriptions = await repository.GetActiveForUserAsync(userId, cancellationToken);
        var sent = 0;
        var expired = 0;
        var failed = 0;

        foreach (var subscription in subscriptions)
        {
            try
            {
                await sender.SendAsync(subscription, message, cancellationToken);
                await repository.MarkSentAsync(subscription.Id, cancellationToken);
                sent++;
            }
            catch (WebPushDeliveryException exception) when (exception.SubscriptionExpired)
            {
                await repository.MarkFailedAsync(subscription.Id, deactivate: true, cancellationToken);
                expired++;
            }
            catch (WebPushDeliveryException)
            {
                await repository.MarkFailedAsync(subscription.Id, deactivate: false, cancellationToken);
                failed++;
            }
        }

        return new WebPushDeliveryResult(subscriptions.Count, sent, expired, failed);
    }

    private Uri ValidateEndpoint(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 ||
            !Uri.TryCreate(value, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps || !sender.IsAllowedEndpoint(endpoint))
        {
            throw new ArgumentException("El endpoint push no pertenece a un proveedor permitido.");
        }

        return endpoint;
    }

    private static void ValidateKey(string value, string name, int expectedBytes, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxCharacters)
            throw new ArgumentException($"La clave {name} no es válida.");
        try
        {
            var normalized = value.Trim().Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
            if (Convert.FromBase64String(normalized).Length != expectedBytes)
                throw new FormatException();
        }
        catch (FormatException)
        {
            throw new ArgumentException($"La clave {name} no es válida.");
        }
    }

    private static WebPushMessage ValidateMessage(SendWebPushRequest request)
    {
        var title = request.Title?.Trim() ?? string.Empty;
        var body = request.Body?.Trim() ?? string.Empty;
        if (title.Length is < 1 or > 120) throw new ArgumentException("El título debe contener entre 1 y 120 caracteres.");
        if (body.Length is < 1 or > 500) throw new ArgumentException("El mensaje debe contener entre 1 y 500 caracteres.");

        var url = string.IsNullOrWhiteSpace(request.Url) ? "/modulo.html?module=notificaciones" : request.Url.Trim();
        if (!url.StartsWith('/') || url.StartsWith("//", StringComparison.Ordinal) || url.Length > 500)
            throw new ArgumentException("La URL de la alerta debe ser una ruta interna.");

        var tag = string.IsNullOrWhiteSpace(request.Tag) ? $"critical-{Guid.NewGuid():N}" : request.Tag.Trim();
        if (tag.Length > 100) throw new ArgumentException("La etiqueta no puede superar 100 caracteres.");
        return new WebPushMessage(title, body, url, tag, request.RequireInteraction);
    }

    private static string? TrimTo(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, maxLength)];
}
