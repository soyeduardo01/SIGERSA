using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIGERSA.Application.InspectionTemplates;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/all-items")]
public sealed class AllItemsController(AllItemsService service) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public Task<IReadOnlyList<AllItem>> Get(CancellationToken cancellationToken) => service.GetAllAsync(cancellationToken);

    [HttpPost]
    [Authorize(Policy = "Administrator")]
    public async Task<ActionResult<AllItem>> Create(AllItemDraft request, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Items }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Administrator")]
    public async Task<IActionResult> Update(int id, AllItemDraft request, CancellationToken cancellationToken)
    {
        await service.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Administrator")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
