using SIGERSA.Domain.Security;

namespace SIGERSA.Domain.Repositories;

public interface IAuthenticationRepository
{
    Task<AuthenticationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task RecordFailedLoginAsync(Guid userId, int maximumAttempts, DateTimeOffset blockedUntil, CancellationToken cancellationToken = default);

    Task RecordSuccessfulLoginAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task InvalidateActiveOtpsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task CreateOtpAsync(Guid userId, string otpHash, DateTimeOffset expiresAt, string? ipHash, CancellationToken cancellationToken = default);

    Task<OtpChallenge?> GetLatestActiveOtpAsync(Guid userId, CancellationToken cancellationToken = default);

    Task RecordFailedOtpAttemptAsync(Guid otpId, int maximumAttempts, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<bool> ConsumeOtpAsync(Guid otpId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task StorePasswordResetProofAsync(Guid proofId, Guid userId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);

    Task StoreRefreshTokenAsync(Guid userId, string tokenHash, Guid familyId, DateTimeOffset expiresAt, string? device, string? ipHash, CancellationToken cancellationToken = default);

    Task<AuthenticationUser?> RotateRefreshTokenAsync(string currentTokenHash, string replacementTokenHash, DateTimeOffset replacementExpiresAt, DateTimeOffset now, string? device, string? ipHash, CancellationToken cancellationToken = default);

    Task<bool> ConsumePasswordResetProofAndUpdatePasswordAsync(Guid proofId, Guid userId, string passwordHash, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(Guid userId, string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default);
}
