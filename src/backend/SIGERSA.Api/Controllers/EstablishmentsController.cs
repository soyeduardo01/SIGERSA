using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIGERSA.Application.Establishments;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/establishments")]
[Authorize(Policy = "Administrator")]
public sealed class EstablishmentsController(EstablishmentService service) : ControllerBase
{
    [HttpGet]
    public Task<EstablishmentsPage> Search(
        string? search,
        string? status,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        service.SearchAsync(search, status, page, pageSize, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<EstablishmentDetails> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpGet("options")]
    public Task<EstablishmentOptions> Options(CancellationToken cancellationToken) =>
        service.GetOptionsAsync(cancellationToken);

    [HttpPost]
    public async Task<ActionResult<object>> Create(
        EstablishmentRequest request,
        CancellationToken cancellationToken)
    {
        var id = await service.CreateAsync(request, UserId(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        EstablishmentRequest request,
        CancellationToken cancellationToken)
    {
        await service.UpdateAsync(id, request, UserId(), cancellationToken);
        return NoContent();
    }

    private Guid UserId() => Guid.TryParse(User.FindFirstValue("sub"), out var userId)
        ? userId
        : throw new UnauthorizedAccessException();
}
