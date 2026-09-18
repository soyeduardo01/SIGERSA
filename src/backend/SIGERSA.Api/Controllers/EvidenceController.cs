using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SIGERSA.Application.Evidences;
using SIGERSA.Domain.Entities;
using SIGERSA.Infrastructure.Configuration;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/evidences")]
[Authorize(Roles = "ADMINISTRADOR,COORDINADOR,TECNICO_EVALUADOR")]
public sealed class EvidenceController(EvidenceService service, IOptions<SupabaseOptions> options) : ControllerBase
{
    [HttpGet]
    public Task<EvidencesPage> Search(
        string? search, string? evidenceType, Guid? evaluationId, int page = 1, int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        service.SearchAsync(search, evidenceType, evaluationId, page, pageSize, ActorContext(), cancellationToken);

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var download = await service.DownloadAsync(id, ActorContext(), cancellationToken);
        return File(download.Content, download.MimeType, download.FileName, enableRangeProcessing: true);
    }

    [HttpPost("upload-authorization")]
    [Authorize(Roles = "TECNICO_EVALUADOR")]
    public Task<Domain.Storage.StorageUploadAuthorization> AuthorizeUpload(
        EvidenceUploadAuthorizationRequest request,
        CancellationToken cancellationToken) =>
        service.AuthorizeUploadAsync(
            request.EvaluationId,
            Actor(),
            request.IdempotencyKey,
            options.Value.DefaultBucketName,
            request.OriginalName,
            request.MimeType,
            request.FileSize,
            cancellationToken);

    [HttpPost("confirm")]
    [Authorize(Roles = "TECNICO_EVALUADOR")]
    public async Task<ActionResult<EvidenceRecord>> ConfirmUpload(
        ConfirmEvidenceUploadRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.ConfirmUploadAsync(
            request.EvaluationId,
            Actor(),
            request.IdempotencyKey,
            options.Value.DefaultBucketName,
            request.SupabasePath,
            request.OriginalName,
            request.MimeType,
            request.EvidenceType,
            request.FileSize,
            request.Sha256Hash,
            request.SourceItem,
            request.Latitude,
            request.Longitude,
            request.AccuracyMeters,
            cancellationToken);
        return Created($"/api/v1/evidences/{result.Id}", result);
    }

    [HttpPost]
    [Authorize(Roles = "TECNICO_EVALUADOR")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6_291_456)]
    public async Task<ActionResult<EvidenceRecord>> Upload(
        [FromForm] EvidenceUploadRequest request,
        CancellationToken cancellationToken)
    {
        await using var content = request.File.OpenReadStream();
        var result = await service.UploadAsync(
            request.EvaluationId,
            Actor(),
            options.Value.DefaultBucketName,
            request.File.FileName,
            request.File.ContentType,
            request.EvidenceType,
            request.SourceItem,
            request.Latitude,
            request.Longitude,
            request.AccuracyMeters,
            request.File.Length,
            content,
            cancellationToken);
        return Created($"/api/v1/evidences/{result.Id}", result);
    }

    private Guid Actor()
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(subject, out var userId)
            ? userId
            : throw new UnauthorizedAccessException();
    }

    private EvidenceActor ActorContext()
    {
        var userId = Actor();
        var companyId = Guid.TryParse(User.FindFirstValue("company_id"), out var parsedCompanyId)
            ? parsedCompanyId
            : (Guid?)null;
        var roles = User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new EvidenceActor(userId, roles, companyId);
    }
}

public sealed record EvidenceUploadAuthorizationRequest(
    Guid EvaluationId,
    Guid IdempotencyKey,
    string OriginalName,
    string MimeType,
    long FileSize);

public sealed record ConfirmEvidenceUploadRequest(
    Guid EvaluationId,
    Guid IdempotencyKey,
    string SupabasePath,
    string OriginalName,
    string MimeType,
    string EvidenceType,
    long FileSize,
    string Sha256Hash,
    int? SourceItem,
    double? Latitude,
    double? Longitude,
    double? AccuracyMeters);

public sealed class EvidenceUploadRequest
{
    public Guid EvaluationId { get; init; }
    public string EvidenceType { get; init; } = "FOTOGRAFIA";
    public int? SourceItem { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? AccuracyMeters { get; init; }
    public required IFormFile File { get; init; }
}
