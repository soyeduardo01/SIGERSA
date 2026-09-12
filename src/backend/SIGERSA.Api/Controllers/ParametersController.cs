using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIGERSA.Application.Parameters;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/parameters")]
public sealed class ParametersController(ParametersService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Administrator")]
    public Task<IReadOnlyList<ParameterControl>> GetAll(string? search, CancellationToken cancellationToken) =>
        service.GetAllAsync(search, cancellationToken);

    [HttpGet("{keyWord}")]
    [Authorize]
    public Task<IReadOnlyList<ParameterControl>> Get(string keyWord, int? companyCode, CancellationToken cancellationToken) =>
        service.GetActiveAsync(keyWord, companyCode, cancellationToken);

    [HttpPost]
    [Authorize(Policy = "Administrator")]
    public async Task<ActionResult<object>> Create(ParameterControlDraft request, CancellationToken cancellationToken)
    {
        var id = await service.CreateAsync(request, Actor(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { keyWord = request.KeyWord, companyCode = request.CompanyCode }, new { parametersId = id });
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "Administrator")]
    public async Task<IActionResult> Update(long id, ParameterControlDraft request, CancellationToken cancellationToken)
    {
        await service.UpdateAsync(id, request, Actor(), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "Administrator")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await service.SoftDeleteAsync(id, Actor(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/activate")]
    [Authorize(Policy = "Administrator")]
    public async Task<IActionResult> Activate(long id, CancellationToken cancellationToken)
    {
        await service.ActivateAsync(id, Actor(), cancellationToken);
        return NoContent();
    }

    private string Actor() => User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();
}
