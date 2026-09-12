using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SIGERSA.Application.Documents;
using SIGERSA.Application.Users;
using SIGERSA.Domain.Security;
using SIGERSA.Infrastructure.Configuration;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "ADMINISTRADOR,ADMINISTRADOR_EMPRESA,COORDINADOR")]
public sealed class UsersController(
    UserManagementService service,
    SupportingDocumentService documents,
    IOptions<SupabaseOptions> storageOptions) : ControllerBase
{
    [HttpGet]
    public Task<ManagedUsersPage> Search(
        string? search,
        string? role,
        string? status,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        service.SearchAsync(search, role, status, page, pageSize, Actor(), cancellationToken);

    [HttpGet("options")]
    public Task<UserManagementOptionsResponse> Options(CancellationToken cancellationToken) =>
        service.GetOptionsAsync(Actor(), cancellationToken);

    [HttpPost]
    [Authorize(Roles = "ADMINISTRADOR,ADMINISTRADOR_EMPRESA")]
    public async Task<ActionResult<object>> Create(
        UserManagementRequest request,
        CancellationToken cancellationToken)
    {
        var id = await service.CreateAsync(request, Actor(), cancellationToken);
        return Created($"/api/v1/users/{id}", new { id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMINISTRADOR,ADMINISTRADOR_EMPRESA")]
    public async Task<IActionResult> Update(
        Guid id,
        UserManagementRequest request,
        CancellationToken cancellationToken)
    {
        await service.UpdateAsync(id, request, Actor(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/suspension")]
    [Authorize(Roles = "ADMINISTRADOR,ADMINISTRADOR_EMPRESA")]
    public async Task<IActionResult> SetSuspension(
        Guid id,
        UserSuspensionRequest request,
        CancellationToken cancellationToken)
    {
        await service.SetSuspendedAsync(
            id,
            request.Suspended,
            request.VersionFila,
            Actor(),
            cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/authorization-letter")]
    [Authorize(Roles = "ADMINISTRADOR,ADMINISTRADOR_EMPRESA")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6_291_456)]
    public async Task<ActionResult<object>> UploadAuthorizationLetter(
        Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var documentId = await documents.UploadUserAuthorizationAsync(
            id, storageOptions.Value.DefaultBucketName, file.FileName, file.ContentType,
            file.Length, content, DocumentActor(), cancellationToken);
        return Created($"/api/v1/users/{id}/authorization-letter/{documentId}", new { id = documentId });
    }

    [HttpGet("{id:guid}/authorization-letter/content")]
    public async Task<IActionResult> DownloadAuthorizationLetter(
        Guid id, CancellationToken cancellationToken)
    {
        var download = await documents.DownloadUserAuthorizationAsync(
            id, DocumentActor(), cancellationToken);
        return File(download.Content, download.MimeType, download.FileName, enableRangeProcessing: true);
    }

    private UserManagementActor Actor()
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
            throw new UnauthorizedAccessException();
        var companyId = Guid.TryParse(User.FindFirstValue("company_id"), out var parsedCompanyId)
            ? parsedCompanyId
            : (Guid?)null;
        var roles = User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new UserManagementActor(userId, roles, companyId);
    }

    private DocumentActor DocumentActor()
    {
        var actor = Actor();
        return new DocumentActor(actor.UserId, actor.Roles, actor.CompanyId);
    }
}

public sealed record UserSuspensionRequest(bool Suspended, long VersionFila);
