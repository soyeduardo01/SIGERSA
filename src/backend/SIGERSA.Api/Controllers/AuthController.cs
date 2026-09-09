using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SIGERSA.Application.Authentication;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public Task<AuthTokensResponse> Login(LoginRequest request, CancellationToken cancellationToken) =>
        authService.LoginAsync(new LoginCommand(request.Email, request.Password, Device(), IpHash()), cancellationToken);

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public Task<AuthTokensResponse> Refresh(RefreshRequest request, CancellationToken cancellationToken) =>
        authService.RefreshAsync(new RefreshTokenCommand(request.RefreshToken, Device(), IpHash()), cancellationToken);

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetGuidClaim(JwtRegisteredClaimNames.Sub, out var userId)) return Unauthorized();
        await authService.LogoutAsync(new LogoutCommand(userId, request.RefreshToken), cancellationToken);
        return NoContent();
    }

    [HttpPost("password-recovery/request")]
    [AllowAnonymous]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult> RequestRecovery(PasswordRecoveryRequest request, CancellationToken cancellationToken)
    {
        await authService.RequestPasswordRecoveryAsync(
            new RequestPasswordRecoveryCommand(request.Email, IpHash()),
            cancellationToken);
        return Accepted(new { message = "Si la cuenta es elegible, recibirá un código de recuperación." });
    }

    [HttpPost("password-recovery/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("otp")]
    public Task<PasswordRecoveryTokenResponse> VerifyRecovery(PasswordRecoveryVerification request, CancellationToken cancellationToken) =>
        authService.VerifyPasswordRecoveryAsync(new VerifyPasswordRecoveryCommand(request.Email, request.Otp), cancellationToken);

    [HttpPost("password-recovery/reset")]
    [Authorize(Policy = "PasswordReset")]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetGuidClaim(JwtRegisteredClaimNames.Sub, out var userId)
            || !TryGetGuidClaim(JwtRegisteredClaimNames.Jti, out var proofId)) return Unauthorized();
        await authService.ResetPasswordAsync(new ResetPasswordCommand(userId, proofId, request.NewPassword), cancellationToken);
        return NoContent();
    }

    private bool TryGetGuidClaim(string claimType, out Guid value) =>
        Guid.TryParse(User.FindFirst(claimType)?.Value, out value);

    private string? Device() => Request.Headers.UserAgent.ToString() is { Length: > 0 } value
        ? value[..Math.Min(value.Length, 250)]
        : null;

    private string? IpHash()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return ip is null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ip))).ToLowerInvariant();
    }
}

public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);
public sealed record PasswordRecoveryRequest(string Email);
public sealed record PasswordRecoveryVerification(string Email, string Otp);
public sealed record ResetPasswordRequest(string NewPassword);
