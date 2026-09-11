using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIGERSA.Application.Scheduling;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/schedules")]
[Authorize(Roles = "ADMINISTRADOR,COORDINADOR,TECNICO_EVALUADOR")]
public sealed class SchedulingController(SchedulingService service) : ControllerBase
{
    [HttpGet]
    public Task<SchedulesPage> Search(string? search, string? status, int page = 1, int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        service.SearchAsync(search, status, page, pageSize, Actor(), cancellationToken);

    [HttpGet("options")]
    public Task<ScheduleOptions> Options(CancellationToken cancellationToken) => service.GetOptionsAsync(Actor(), cancellationToken);

    [HttpPost]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR")]
    public async Task<ActionResult<object>> Create(ScheduleInput input,
        [FromHeader(Name = "Idempotency-Key")] Guid? headerKey, CancellationToken cancellationToken)
    {
        if (headerKey.HasValue && input.IdempotencyKey != Guid.Empty && headerKey != input.IdempotencyKey)
            throw new ArgumentException("Las claves de idempotencia no coinciden.");
        var id = await service.CreateAsync(input with { IdempotencyKey = headerKey ?? input.IdempotencyKey }, Actor(), cancellationToken);
        return Created($"/api/v1/schedules/{id}", new { id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR")]
    public async Task<IActionResult> Update(Guid id, ScheduleInput input, CancellationToken cancellationToken)
    {
        await service.UpdateAsync(id, input, Actor(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR")]
    public async Task<IActionResult> Cancel(Guid id, CancelScheduleInput input, CancellationToken cancellationToken)
    {
        await service.CancelAsync(id, input, Actor(), cancellationToken);
        return NoContent();
    }

    private SchedulingActor Actor()
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId)) throw new UnauthorizedAccessException();
        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal).ToArray();
        return new SchedulingActor(userId, roles);
    }
}
