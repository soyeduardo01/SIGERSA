using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIGERSA.Application.Operations;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
public sealed class ReportsController(ReportService service) : ControllerBase
{
    [HttpPost("evaluations/{evaluationId:guid}/generate")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR")]
    public Task<ReportFileReference> Generate(
        Guid evaluationId, GenerateReportRequest request, CancellationToken cancellationToken) =>
        service.GenerateAsync(evaluationId, request.Official, Actor(), cancellationToken);

    [HttpGet("{reportId:guid}/content")]
    [Authorize(Roles = "ADMINISTRADOR,ADMINISTRADOR_EMPRESA,USUARIO_DELEGADO,COORDINADOR,TECNICO_EVALUADOR")]
    public async Task<IActionResult> Download(Guid reportId, CancellationToken cancellationToken)
    {
        var download = await service.DownloadAsync(reportId, Actor(), cancellationToken);
        return File(download.Content, download.MimeType, download.FileName, enableRangeProcessing: true);
    }

    private OperationalActor Actor()
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var userId = Guid.TryParse(subject, out var parsedUserId)
            ? parsedUserId
            : throw new UnauthorizedAccessException();
        var companyId = Guid.TryParse(User.FindFirstValue("company_id"), out var parsedCompanyId)
            ? parsedCompanyId
            : (Guid?)null;
        var roles = User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new OperationalActor(userId, roles, companyId);
    }
}

public sealed record GenerateReportRequest(bool Official = false);
