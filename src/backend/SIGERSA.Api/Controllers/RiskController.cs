using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIGERSA.Application.Risk;
using SIGERSA.Domain.Risk;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/risk")]
[Authorize]
public sealed class RiskController(InspectionRiskService service) : ControllerBase
{
    [HttpPost("all-items/calculate")]
    public Task<IReadOnlyList<AllItemScore>> Calculate(
        CalculateInspectionRiskRequest request,
        CancellationToken cancellationToken) =>
        service.CalculateAsync(request.Ratings, cancellationToken);
}

public sealed record CalculateInspectionRiskRequest(IReadOnlyList<InspectionRatingRequest> Ratings);
