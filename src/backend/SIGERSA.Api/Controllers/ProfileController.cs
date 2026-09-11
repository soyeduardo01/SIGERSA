using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIGERSA.Application.Profiles;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/profile")]
[Authorize]
public sealed class ProfileController(ProfileService service) : ControllerBase
{
    [HttpGet]
    public Task<ProfileResponse> Get(CancellationToken cancellationToken) =>
        service.GetAsync(UserId(), cancellationToken);

    [HttpPut]
    public Task<ProfileResponse> Update(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(UserId(), request, cancellationToken);

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        await service.ChangePasswordAsync(UserId(), request, cancellationToken);
        return NoContent();
    }

    private Guid UserId() => Guid.TryParse(User.FindFirstValue("sub"), out var userId)
        ? userId
        : throw new UnauthorizedAccessException();
}
