using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SIGERSA.Application.Operations;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/public/complaints")]
[AllowAnonymous]
[EnableRateLimiting("public-complaints")]
public sealed class PublicComplaintsController(PublicComplaintService service) : ControllerBase
{
    [HttpGet("options")]
    public Task<IReadOnlyList<OperationalOption>> Options(CancellationToken cancellationToken) =>
        service.GetOptionsAsync(cancellationToken);

    [HttpPost]
    public async Task<ActionResult<object>> Create(
        PublicComplaintRequest request,
        CancellationToken cancellationToken)
    {
        var id = await service.CreateAsync(request, cancellationToken);
        return Accepted(new { id, status = "ASIGNADA_COORDINADOR" });
    }
}
