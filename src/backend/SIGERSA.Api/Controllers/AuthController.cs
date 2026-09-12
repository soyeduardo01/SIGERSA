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
    public async Task<ActionResult<object>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(
            new LoginCommand(request.Email, request.Password, Device(), IpHash()), cancellationToken);
        return result.RequiresTwoFactor
            ? Accepted(new
            {
                requiresTwoFactor = true,
                expiresAt = result.ExpiresAt,
                provider = result.Provider
            })
            : Ok(result.Session);
    }

    [HttpPost("login/verify-2fa")]
    [AllowAnonymous]
    [EnableRateLimiting("otp-verify")]
    public Task<AuthTokensResponse> VerifyTwoFactor(
        TwoFactorVerification request, CancellationToken cancellationToken) =>
        authService.VerifyTwoFactorAsync(
            new VerifyTwoFactorCommand(request.Email, request.Otp, Device(), IpHash()), cancellationToken);

    [HttpPost("login/verify-supabase-mfa")]
    [AllowAnonymous]
    [EnableRateLimiting("otp-verify")]
    public Task<AuthTokensResponse> VerifySupabaseMfa(
        SupabaseMfaVerification request,
        CancellationToken cancellationToken) =>
        authService.VerifySupabaseMfaAsync(
            new VerifySupabaseMfaCommand(
                request.Email,
                request.SupabaseAccessToken,
                Device(),
                IpHash()),
            cancellationToken);

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
    [EnableRateLimiting("otp-send")]
    public async Task<IActionResult> RequestRecovery(PasswordRecoveryRequest request, CancellationToken cancellationToken)
    {
        await authService.RequestPasswordRecoveryAsync(
            new RequestPasswordRecoveryCommand(request.Email, IpHash()),
            cancellationToken);
        return Accepted(new { message = "Si la cuenta es elegible, recibirá un código de recuperación." });
    }

    [HttpPost("password-recovery/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("otp-verify")]
    public Task<PasswordRecoveryTokenResponse> VerifyRecovery(PasswordRecoveryVerification request, CancellationToken cancellationToken) =>
        authService.VerifyPasswordRecoveryAsync(new VerifyPasswordRecoveryCommand(request.Email, request.Otp), cancellationToken);

    [HttpPost("password-recovery/reset")]
    [Authorize(Policy = "PasswordReset")]
    [EnableRateLimiting("otp-verify")]
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
public sealed record TwoFactorVerification(string Email, string Otp);
public sealed record SupabaseMfaVerification(string Email, string SupabaseAccessToken);
public sealed record RefreshRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);
public sealed record PasswordRecoveryRequest(string Email);
public sealed record PasswordRecoveryVerification(string Email, string Otp);
public sealed record ResetPasswordRequest(string NewPassword);
