using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIGERSA.Application.Operations;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize(Roles = "ADMINISTRADOR,ADMINISTRADOR_EMPRESA,USUARIO_DELEGADO,COORDINADOR,TECNICO_EVALUADOR,LABORATORISTA")]
public sealed class OperationsController(OperationalService service) : ControllerBase
{
    [HttpGet("dashboard")]
    public Task<DashboardSnapshot> Dashboard(CancellationToken cancellationToken) =>
        service.GetDashboardAsync(Actor(), cancellationToken);

    [HttpGet("notifications")]
    public Task<IReadOnlyList<NotificationRecord>> Notifications(CancellationToken cancellationToken) =>
        service.GetNotificationsAsync(Actor(), cancellationToken);

    [HttpPost("notifications/{notificationId:guid}/read")]
    public async Task<IActionResult> MarkNotificationRead(
        Guid notificationId, CancellationToken cancellationToken)
    {
        await service.MarkNotificationReadAsync(notificationId, Actor(), cancellationToken);
        return NoContent();
    }

    [HttpGet("surveillance")]
    public Task<SurveillancePage> Surveillance(
        string? search, string? kind, string? result, int page = 1, int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        service.SearchSurveillanceAsync(search, kind, result, page, pageSize, Actor(), cancellationToken);

    [HttpGet("surveillance/options")]
    public Task<SurveillanceOptions> SurveillanceOptions(CancellationToken cancellationToken) =>
        service.GetSurveillanceOptionsAsync(Actor(), cancellationToken);

    [HttpPost("surveillance")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR,LABORATORISTA")]
    public async Task<ActionResult<object>> CreateSurveillance(
        SurveillanceRequest request, CancellationToken cancellationToken)
    {
        var id = await service.CreateSurveillanceAsync(request, Actor(), cancellationToken);
        return Created($"/api/v1/surveillance/{id}", new { id });
    }

    [HttpPut("surveillance/{id:guid}")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR,LABORATORISTA")]
    public async Task<IActionResult> UpdateSurveillance(
        Guid id, SurveillanceRequest request, CancellationToken cancellationToken)
    {
        await service.UpdateSurveillanceAsync(id, request, Actor(), cancellationToken);
        return NoContent();
    }

    [HttpGet("findings")]
    public Task<FindingsPage> Findings(
        string? search, string? status, int page = 1, int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        service.SearchFindingsAsync(search, status, page, pageSize, Actor(), cancellationToken);

    [HttpGet("findings/{id:guid}")]
    public Task<FindingDetail> Finding(Guid id, CancellationToken cancellationToken) =>
        service.GetFindingAsync(id, Actor(), cancellationToken);

    [HttpGet("findings/options")]
    public Task<FindingOptions> FindingOptions(CancellationToken cancellationToken) =>
        service.GetFindingOptionsAsync(Actor(), cancellationToken);

    [HttpPost("findings/{id:guid}/close")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR")]
    public async Task<IActionResult> CloseFinding(
        Guid id, CloseFindingRequest request, CancellationToken cancellationToken)
    {
        await service.CloseFindingAsync(id, request, Actor(), cancellationToken);
        return NoContent();
    }

    [HttpGet("history/evaluations")]
    public Task<HistoricalEvaluationsPage> History(
        string? search, string? status, DateTimeOffset? from, DateTimeOffset? to,
        int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        service.SearchHistoryAsync(search, status, from, to, page, pageSize, Actor(), cancellationToken);

    [HttpGet("history/evaluations/{evaluationId:guid}/timeline")]
    public Task<IReadOnlyList<TimelineEvent>> Timeline(
        Guid evaluationId, CancellationToken cancellationToken) =>
        service.GetTimelineAsync(evaluationId, Actor(), cancellationToken);

    [HttpGet("audit-events")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR")]
    public Task<AuditEventsPage> Audit(
        string? search, string? result, DateTimeOffset? from, DateTimeOffset? to,
        int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        service.SearchAuditAsync(search, result, from, to, page, pageSize, Actor(), cancellationToken);

    private OperationalActor Actor()
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var userId = Guid.TryParse(subject, out var parsedUserId)
            ? parsedUserId
            : throw new UnauthorizedAccessException();
        var companyId = Guid.TryParse(User.FindFirstValue("company_id"), out var parsedCompanyId)
            ? parsedCompanyId
            : (Guid?)null;
        var roles = User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new OperationalActor(userId, roles, companyId);
    }
}
