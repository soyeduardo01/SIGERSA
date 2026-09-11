using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIGERSA.Application.Companies;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/companies")]
[Authorize(Policy = "Administrator")]
public sealed class CompaniesController(CompanyService service) : ControllerBase
{
    [HttpGet]
    public Task<CompaniesPage> Search(
        string? search,
        string? status,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        service.SearchAsync(search, status, page, pageSize, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<object>> Create(CompanyRequest request, CancellationToken cancellationToken)
    {
        var id = await service.CreateAsync(request, UserId(), cancellationToken);
        return Created($"/api/v1/companies/{id}", new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CompanyRequest request, CancellationToken cancellationToken)
    {
        await service.UpdateAsync(id, request, UserId(), cancellationToken);
        return NoContent();
    }

    private Guid UserId() => Guid.TryParse(User.FindFirstValue("sub"), out var userId)
        ? userId
        : throw new UnauthorizedAccessException();
}
