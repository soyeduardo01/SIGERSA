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
}
