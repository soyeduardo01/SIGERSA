namespace SIGERSA.Domain.Security;

public interface IPasswordService
{
    string Hash(string secret);

    bool Verify(string hash, string secret);
}

public interface ITokenService
{
    IssuedToken CreateAccessToken(AuthenticationUser user, DateTimeOffset now);

    IssuedToken CreatePasswordResetToken(AuthenticationUser user, DateTimeOffset now);
}

public interface IEmailSender
{
    Task SendPasswordRecoveryOtpAsync(
        string recipient,
        string recipientName,
        string otp,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task SendTwoFactorOtpAsync(
        string recipient,
        string recipientName,
        string otp,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);
}

public interface ISupabaseMfaGateway
{
    Task<Guid> EnsureUserAsync(
        Guid? supabaseUserId,
        string email,
        string password,
        string fullName,
        CancellationToken cancellationToken = default);

    Task UpdateUserAsync(
        Guid supabaseUserId,
        string? email,
        string? password,
        CancellationToken cancellationToken = default);

    Task<Guid> ValidateAal2TokenAsync(
        string accessToken,
        CancellationToken cancellationToken = default);
}
