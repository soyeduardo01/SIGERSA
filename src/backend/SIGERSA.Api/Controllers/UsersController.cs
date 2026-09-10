using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIGERSA.Application.Users;
using SIGERSA.Domain.Security;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "ADMINISTRADOR,ADMINISTRADOR_EMPRESA,COORDINADOR")]
public sealed class UsersController(UserManagementService service) : ControllerBase
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
}

public sealed record UserSuspensionRequest(bool Suspended, long VersionFila);
