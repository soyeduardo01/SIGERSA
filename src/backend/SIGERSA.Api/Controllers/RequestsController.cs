using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIGERSA.Application.Requests;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/requests")]
[Authorize(Roles = "ADMINISTRADOR,ADMINISTRADOR_EMPRESA,USUARIO_DELEGADO,COORDINADOR,TECNICO_EVALUADOR")]
public sealed class RequestsController(InspectionRequestService service) : ControllerBase
{
    [HttpGet]
    public Task<InspectionRequestsPage> Search(
        string? search,
        string? status,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        service.SearchAsync(search, status, page, pageSize, Actor(), cancellationToken);

    [HttpGet("options")]
    public Task<InspectionRequestOptions> Options(CancellationToken cancellationToken) =>
        service.GetOptionsAsync(Actor(), cancellationToken);

    [HttpPost]
    [Authorize(Roles = "ADMINISTRADOR,ADMINISTRADOR_EMPRESA,USUARIO_DELEGADO,COORDINADOR")]
    public async Task<ActionResult<object>> Create(
        InspectionRequestInput input,
        [FromHeader(Name = "Idempotency-Key")] Guid? headerIdempotencyKey,
        CancellationToken cancellationToken)
    {
        if (headerIdempotencyKey.HasValue && input.IdempotencyKey != Guid.Empty &&
            headerIdempotencyKey != input.IdempotencyKey)
        {
            throw new ArgumentException("Las claves de idempotencia del encabezado y el cuerpo no coinciden.");
        }
        var effectiveInput = input with
        {
            IdempotencyKey = headerIdempotencyKey ?? input.IdempotencyKey
        };
        var id = await service.CreateAsync(effectiveInput, Actor(), cancellationToken);
        return Created($"/api/v1/requests/{id}", new { id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMINISTRADOR,ADMINISTRADOR_EMPRESA,USUARIO_DELEGADO,COORDINADOR")]
    public async Task<IActionResult> Update(
        Guid id,
        InspectionRequestInput input,
        CancellationToken cancellationToken)
    {
        await service.UpdateAsync(id, input, Actor(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = "ADMINISTRADOR,ADMINISTRADOR_EMPRESA,USUARIO_DELEGADO,COORDINADOR")]
    public async Task<IActionResult> Submit(
        Guid id,
        InspectionRequestTransitionInput input,
        CancellationToken cancellationToken)
    {
        await service.TransitionAsync(id, input, "ENVIADA", Actor(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "ADMINISTRADOR,ADMINISTRADOR_EMPRESA,USUARIO_DELEGADO,COORDINADOR")]
    public async Task<IActionResult> Cancel(
        Guid id,
        InspectionRequestTransitionInput input,
        CancellationToken cancellationToken)
    {
        await service.TransitionAsync(id, input, "CANCELADA", Actor(), cancellationToken);
        return NoContent();
    }

    private InspectionRequestActor Actor()
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
            throw new UnauthorizedAccessException();
        var companyId = Guid.TryParse(User.FindFirstValue("company_id"), out var parsedCompanyId)
            ? parsedCompanyId
            : (Guid?)null;
        var roles = User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new InspectionRequestActor(userId, roles, companyId);
    }
}
