using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SIGERSA.Application.Notifications;
using SIGERSA.Domain.Notifications;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/push")]
[Authorize]
public sealed class WebPushController(WebPushNotificationService service) : ControllerBase
{
    [HttpGet("configuration")]
    public ActionResult<WebPushConfiguration> Configuration() => Ok(service.GetConfiguration());

    [HttpPut("subscriptions")]
    public async Task<IActionResult> Subscribe(
        WebPushSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        await service.SubscribeAsync(CurrentUserId(), request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("subscriptions")]
    public async Task<IActionResult> Unsubscribe(
        RemoveWebPushSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        await service.UnsubscribeAsync(CurrentUserId(), request.Endpoint, cancellationToken);
        return NoContent();
    }

    [HttpPost("users/{userId:guid}/alerts")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR")]
    [EnableRateLimiting("push-send")]
    public Task<WebPushDeliveryResult> SendToUser(
        Guid userId,
        SendWebPushRequest request,
        CancellationToken cancellationToken) =>
        service.SendToUserAsync(userId, request, cancellationToken);

    private Guid CurrentUserId()
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(subject, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}

public sealed record RemoveWebPushSubscriptionRequest(string Endpoint);
