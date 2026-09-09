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
    [HttpPost("upload-authorization")]
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
            cancellationToken);
        return Created($"/api/v1/evidences/{result.Id}", result);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(26_214_400)]
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
    string Sha256Hash);

public sealed class EvidenceUploadRequest
{
    public Guid EvaluationId { get; init; }
    public string EvidenceType { get; init; } = "FOTOGRAFIA";
    public required IFormFile File { get; init; }
}
