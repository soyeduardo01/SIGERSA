using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIGERSA.Application.Corrections;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/corrections")]
[Authorize(Roles = "ADMINISTRADOR,ADMINISTRADOR_EMPRESA,USUARIO_DELEGADO,COORDINADOR,TECNICO_EVALUADOR")]
public sealed class CorrectionsController(CorrectionService service) : ControllerBase
{
    [HttpGet]
    public Task<CorrectionsPage> Search(string? search, string? status, int page = 1, int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        service.SearchAsync(search, status, page, pageSize, Actor(), cancellationToken);

    [HttpGet("options")]
    public Task<CorrectionOptions> Options(CancellationToken cancellationToken) =>
        service.GetOptionsAsync(Actor(), cancellationToken);

    [HttpPost]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR")]
    public async Task<ActionResult<object>> Create(
        CorrectionInput input, [FromHeader(Name = "Idempotency-Key")] Guid? headerKey,
        CancellationToken cancellationToken)
    {
        if (headerKey.HasValue && input.IdempotencyKey != Guid.Empty && headerKey != input.IdempotencyKey)
            throw new ArgumentException("Las claves de idempotencia no coinciden.");
        var id = await service.CreateAsync(input with { IdempotencyKey = headerKey ?? input.IdempotencyKey }, Actor(), cancellationToken);
        return Created($"/api/v1/corrections/{id}", new { id });
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = "ADMINISTRADOR,ADMINISTRADOR_EMPRESA,USUARIO_DELEGADO,TECNICO_EVALUADOR")]
    public async Task<IActionResult> Submit(Guid id, CorrectionTransitionInput input, CancellationToken cancellationToken)
    {
        await service.SubmitAsync(id, input, Actor(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/accept")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR")]
    public Task<IActionResult> Accept(Guid id, CorrectionTransitionInput input, CancellationToken cancellationToken) =>
        Resolve(id, input, "ACEPTADA", cancellationToken);

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR")]
    public Task<IActionResult> Reject(Guid id, CorrectionTransitionInput input, CancellationToken cancellationToken) =>
        Resolve(id, input, "RECHAZADA", cancellationToken);

    private async Task<IActionResult> Resolve(Guid id, CorrectionTransitionInput input, string status, CancellationToken cancellationToken)
    {
        await service.ResolveAsync(id, input, status, Actor(), cancellationToken);
        return NoContent();
    }

    private CorrectionActor Actor()
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId)) throw new UnauthorizedAccessException();
        var companyId = Guid.TryParse(User.FindFirstValue("company_id"), out var parsedCompanyId) ? parsedCompanyId : (Guid?)null;
        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToArray();
        return new CorrectionActor(userId, roles, companyId);
    }
}
