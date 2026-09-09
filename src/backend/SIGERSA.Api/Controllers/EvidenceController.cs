using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SIGERSA.Application.Evidences;
using SIGERSA.Domain.Entities;
using SIGERSA.Infrastructure.Configuration;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/evidences")]
[Authorize(Roles = "ADMINISTRADOR,TECNICO_EVALUADOR")]
public sealed class EvidenceController(EvidenceService service, IOptions<SupabaseOptions> options) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(26_214_400)]
    public async Task<ActionResult<EvidenceRecord>> Upload(
        [FromForm] EvidenceUploadRequest request,
        CancellationToken cancellationToken)
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subject, out var userId)) return Unauthorized();
        await using var content = request.File.OpenReadStream();
        var result = await service.UploadAsync(
            request.EvaluationId,
            userId,
            options.Value.DefaultBucketName,
            request.File.FileName,
            request.File.ContentType,
            request.EvidenceType,
            content,
            cancellationToken);
        return Created($"/api/v1/evidences/{result.Id}", result);
    }
}

public sealed class EvidenceUploadRequest
{
    public Guid EvaluationId { get; init; }
    public string EvidenceType { get; init; } = "FOTOGRAFIA";
    public required IFormFile File { get; init; }
}
