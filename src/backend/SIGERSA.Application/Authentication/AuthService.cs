using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.Extensions.Options;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Security;

namespace SIGERSA.Application.Authentication;

public sealed class AuthService(
    IAuthenticationRepository repository,
    IPasswordService passwordService,
    ITokenService tokenService,
    IEmailSender emailSender,
    IOptions<AuthFlowOptions> options,
    TimeProvider timeProvider,
    IValidator<LoginCommand> loginValidator,
    IValidator<RefreshTokenCommand> refreshValidator,
    IValidator<LogoutCommand> logoutValidator,
    IValidator<RequestPasswordRecoveryCommand> requestRecoveryValidator,
    IValidator<VerifyPasswordRecoveryCommand> verifyRecoveryValidator,
    IValidator<ResetPasswordCommand> resetPasswordValidator) : IAuthService
{
    private const string GenericAuthenticationError = "Las credenciales no son válidas.";
    private readonly AuthFlowOptions _options = options.Value;

    public async Task<AuthTokensResponse> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        await loginValidator.ValidateAndThrowAsync(command, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var user = await repository.FindByEmailAsync(NormalizeEmail(command.Email), cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException(GenericAuthenticationError);
        }

        if (!user.Activo || user.Estado is not ("ACTIVO" or "BLOQUEADO") || user.BloqueadoHasta > now)
        {
            throw new UnauthorizedAccessException(GenericAuthenticationError);
        }

        if (!passwordService.Verify(user.PasswordHash, command.Password))
        {
            await repository.RecordFailedLoginAsync(
                user.Id,
                _options.MaximumLoginAttempts,
                now.AddMinutes(_options.LockoutMinutes),
                cancellationToken);
            throw new UnauthorizedAccessException(GenericAuthenticationError);
        }

        await repository.RecordSuccessfulLoginAsync(user.Id, now, cancellationToken);
        return await IssueSessionAsync(user, command.Device, command.IpHash, now, cancellationToken);
    }

    public async Task<AuthTokensResponse> RefreshAsync(
        RefreshTokenCommand command,
        CancellationToken cancellationToken = default)
    {
        await refreshValidator.ValidateAndThrowAsync(command, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var replacement = CreateOpaqueToken();
        var replacementHash = HashOpaqueToken(replacement);
        var replacementExpiresAt = now.AddDays(_options.RefreshTokenDays);

        var user = await repository.RotateRefreshTokenAsync(
            HashOpaqueToken(command.RefreshToken),
            replacementHash,
            replacementExpiresAt,
            now,
            command.Device,
            command.IpHash,
            cancellationToken);

        if (user is null || !user.Activo || user.Estado is not "ACTIVO")
        {
            throw new UnauthorizedAccessException("El refresh token no es válido.");
        }

        var access = tokenService.CreateAccessToken(user, now);
        return new AuthTokensResponse(
            access.Value,
            access.ExpiresAt,
            replacement,
            replacementExpiresAt,
            user.Roles);
    }

    public async Task LogoutAsync(LogoutCommand command, CancellationToken cancellationToken = default)
    {
        await logoutValidator.ValidateAndThrowAsync(command, cancellationToken);
        await repository.RevokeRefreshTokenAsync(
            command.UserId,
            HashOpaqueToken(command.RefreshToken),
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public async Task RequestPasswordRecoveryAsync(
        RequestPasswordRecoveryCommand command,
        CancellationToken cancellationToken = default)
    {
        await requestRecoveryValidator.ValidateAndThrowAsync(command, cancellationToken);
        var user = await repository.FindByEmailAsync(NormalizeEmail(command.Email), cancellationToken);
        if (user is null || !user.Activo || user.Estado is not "ACTIVO")
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.OtpLifetimeMinutes);
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(System.Globalization.CultureInfo.InvariantCulture);

        await repository.InvalidateActiveOtpsAsync(user.Id, now, cancellationToken);
        await repository.CreateOtpAsync(
            user.Id,
            passwordService.Hash(otp),
            expiresAt,
            command.IpHash,
            cancellationToken);
        try
        {
            await emailSender.SendPasswordRecoveryOtpAsync(
                user.Correo,
                user.NombreCompleto,
                otp,
                expiresAt,
                cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            // Nunca deje un código utilizable si el correo no pudo entregarse.
            await repository.InvalidateActiveOtpsAsync(user.Id, timeProvider.GetUtcNow(), cancellationToken);
            throw;
        }
    }

    public async Task<PasswordRecoveryTokenResponse> VerifyPasswordRecoveryAsync(
        VerifyPasswordRecoveryCommand command,
        CancellationToken cancellationToken = default)
    {
        await verifyRecoveryValidator.ValidateAndThrowAsync(command, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var user = await repository.FindByEmailAsync(NormalizeEmail(command.Email), cancellationToken);
        if (user is null || !user.Activo || user.Estado is not "ACTIVO")
        {
            throw new UnauthorizedAccessException("El código no es válido.");
        }

        var challenge = await repository.GetLatestActiveOtpAsync(user.Id, cancellationToken);
        if (challenge is null || challenge.ExpiraEn <= now || !passwordService.Verify(challenge.OtpHash, command.Otp))
        {
            if (challenge is not null)
            {
                await repository.RecordFailedOtpAttemptAsync(
                    challenge.Id,
                    _options.MaximumOtpAttempts,
                    now,
                    cancellationToken);
            }

            throw new UnauthorizedAccessException("El código no es válido.");
        }

        if (!await repository.ConsumeOtpAsync(challenge.Id, now, cancellationToken))
        {
            throw new UnauthorizedAccessException("El código no es válido.");
        }

        var resetToken = tokenService.CreatePasswordResetToken(user, now);
        await repository.StorePasswordResetProofAsync(
            resetToken.TokenId,
            user.Id,
            resetToken.ExpiresAt,
            cancellationToken);
        return new PasswordRecoveryTokenResponse(resetToken.Value, resetToken.ExpiresAt);
    }

    public async Task ResetPasswordAsync(
        ResetPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        await resetPasswordValidator.ValidateAndThrowAsync(command, cancellationToken);
        var updated = await repository.ConsumePasswordResetProofAndUpdatePasswordAsync(
            command.ProofId,
            command.UserId,
            passwordService.Hash(command.NewPassword),
            timeProvider.GetUtcNow(),
            cancellationToken);
        if (!updated)
        {
            throw new UnauthorizedAccessException("El comprobante de recuperación no es válido o ya fue utilizado.");
        }
    }

    private async Task<AuthTokensResponse> IssueSessionAsync(
        AuthenticationUser user,
        string? device,
        string? ipHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var access = tokenService.CreateAccessToken(user, now);
        var refresh = CreateOpaqueToken();
        var refreshExpiresAt = now.AddDays(_options.RefreshTokenDays);
        await repository.StoreRefreshTokenAsync(
            user.Id,
            HashOpaqueToken(refresh),
            Guid.NewGuid(),
            refreshExpiresAt,
            device,
            ipHash,
            cancellationToken);

        return new AuthTokensResponse(
            access.Value,
            access.ExpiresAt,
            refresh,
            refreshExpiresAt,
            user.Roles);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string CreateOpaqueToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashOpaqueToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    }
}
