using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIGERSA.Application.Cases;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/cases")]
[Authorize(Roles = "ADMINISTRADOR,COORDINADOR,TECNICO_EVALUADOR")]
public sealed class CasesController(CaseService service) : ControllerBase
{
    [HttpGet]
    public Task<CasesPage> Search(string? search, string? status, int page = 1, int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        service.SearchAsync(search, status, page, pageSize, Actor(), cancellationToken);

    [HttpGet("options")]
    public Task<CaseOptions> Options(CancellationToken cancellationToken) =>
        service.GetOptionsAsync(Actor(), cancellationToken);

    [HttpPost]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR")]
    public async Task<ActionResult<object>> Create(
        CaseInput input,
        [FromHeader(Name = "Idempotency-Key")] Guid? headerKey,
        CancellationToken cancellationToken)
    {
        if (headerKey.HasValue && input.IdempotencyKey != Guid.Empty && headerKey != input.IdempotencyKey)
            throw new ArgumentException("Las claves de idempotencia no coinciden.");
        var id = await service.CreateAsync(input with { IdempotencyKey = headerKey ?? input.IdempotencyKey }, Actor(), cancellationToken);
        return Created($"/api/v1/cases/{id}", new { id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR")]
    public async Task<IActionResult> Update(Guid id, CaseInput input, CancellationToken cancellationToken)
    {
        await service.UpdateAsync(id, input, Actor(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR")]
    public async Task<IActionResult> Close(Guid id, CloseCaseInput input, CancellationToken cancellationToken)
    {
        await service.CloseAsync(id, input, Actor(), cancellationToken);
        return NoContent();
    }

    private CaseActor Actor()
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId)) throw new UnauthorizedAccessException();
        var companyId = Guid.TryParse(User.FindFirstValue("company_id"), out var parsed) ? parsed : (Guid?)null;
        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal).ToArray();
        return new CaseActor(userId, roles, companyId);
    }
}
