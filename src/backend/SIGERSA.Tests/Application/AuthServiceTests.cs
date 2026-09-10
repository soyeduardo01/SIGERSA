using FluentValidation;
using Microsoft.Extensions.Options;
using SIGERSA.Application.Authentication;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Security;
using SIGERSA.Infrastructure.Security;

namespace SIGERSA.Tests.Application;

public sealed class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoginShouldIssueAndPersistRotatingSessionTokens()
    {
        var passwords = new Pbkdf2PasswordService();
        var repository = new FakeAuthenticationRepository
        {
            User = ActiveUser(passwords.Hash("Valid-Password-2026!"))
        };
        var service = CreateService(repository, passwords, new FakeEmailSender());

        var result = await service.LoginAsync(
            new LoginCommand("USER@EXAMPLE.COM", "Valid-Password-2026!", "test", "ip"));

        Assert.Equal("access-token", result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.NotNull(repository.StoredRefreshHash);
        Assert.DoesNotContain(result.RefreshToken, repository.StoredRefreshHash, StringComparison.Ordinal);
        Assert.True(repository.SuccessfulLoginRecorded);
    }

    [Fact]
    public async Task LoginShouldRecordAFailedAttemptWithoutDisclosingCause()
    {
        var passwords = new Pbkdf2PasswordService();
        var repository = new FakeAuthenticationRepository
        {
            User = ActiveUser(passwords.Hash("Valid-Password-2026!"))
        };
        var service = CreateService(repository, passwords, new FakeEmailSender());

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(new LoginCommand("user@example.com", "Wrong-Password-2026!", null, null)));

        Assert.Equal("Las credenciales no son válidas.", exception.Message);
        Assert.Equal(1, repository.FailedLogins);
    }

    [Fact]
    public async Task RecoveryShouldStoreOnlyAHashAndSendSixDigitOtp()
    {
        var passwords = new Pbkdf2PasswordService();
        var repository = new FakeAuthenticationRepository { User = ActiveUser(passwords.Hash("Valid-Password-2026!")) };
        var email = new FakeEmailSender();
        var service = CreateService(repository, passwords, email);

        await service.RequestPasswordRecoveryAsync(new RequestPasswordRecoveryCommand("user@example.com", "ip"));

        Assert.Matches("^[0-9]{6}$", email.Otp!);
        Assert.NotEqual(email.Otp, repository.StoredOtpHash);
        Assert.True(passwords.Verify(repository.StoredOtpHash!, email.Otp!));
        Assert.True(repository.PreviousOtpsInvalidated);
    }

    [Fact]
    public async Task RecoveryShouldInvalidateOtpWhenEmailDeliveryFails()
    {
        var passwords = new Pbkdf2PasswordService();
        var repository = new FakeAuthenticationRepository { User = ActiveUser(passwords.Hash("Valid-Password-2026!")) };
        var email = new FakeEmailSender { ShouldFail = true };
        var service = CreateService(repository, passwords, email);

        await Assert.ThrowsAsync<EmailDeliveryException>(() =>
            service.RequestPasswordRecoveryAsync(new RequestPasswordRecoveryCommand("user@example.com", "ip")));

        Assert.Equal(2, repository.OtpInvalidations);
    }

    [Fact]
    public async Task VerifyRecoveryShouldConsumeOtpAndIssueSinglePurposeToken()
    {
        var passwords = new Pbkdf2PasswordService();
        var repository = new FakeAuthenticationRepository
        {
            User = ActiveUser(passwords.Hash("Valid-Password-2026!")),
            Challenge = new OtpChallenge(Guid.NewGuid(), Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), passwords.Hash("123456"), Now.AddMinutes(5), 0, "ACTIVO")
        };
        var service = CreateService(repository, passwords, new FakeEmailSender());

        var result = await service.VerifyPasswordRecoveryAsync(
            new VerifyPasswordRecoveryCommand("user@example.com", "123456"));

        Assert.Equal("reset-token", result.ResetToken);
        Assert.True(repository.OtpConsumed);
        Assert.Equal(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), repository.StoredProofId);
    }

    [Fact]
    public async Task ResetPasswordShouldRejectAConsumedOrExpiredProof()
    {
        var passwords = new Pbkdf2PasswordService();
        var repository = new FakeAuthenticationRepository
        {
            User = ActiveUser(passwords.Hash("Valid-Password-2026!")),
            PasswordResetAccepted = false
        };
        var service = CreateService(repository, passwords, new FakeEmailSender());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ResetPasswordAsync(
            new ResetPasswordCommand(repository.User.Id, Guid.NewGuid(), "New-Password-2026!")));
    }

    [Fact]
    public async Task LogoutShouldRevokeOnlyThePresentedRefreshTokenHash()
    {
        var passwords = new Pbkdf2PasswordService();
        var repository = new FakeAuthenticationRepository { User = ActiveUser(passwords.Hash("Valid-Password-2026!")) };
        var service = CreateService(repository, passwords, new FakeEmailSender());

        await service.LogoutAsync(new LogoutCommand(repository.User.Id, "refresh-token"));

        Assert.NotNull(repository.RevokedRefreshHash);
        Assert.DoesNotContain("refresh-token", repository.RevokedRefreshHash, StringComparison.Ordinal);
    }

    private static AuthService CreateService(FakeAuthenticationRepository repository, IPasswordService passwords, FakeEmailSender email) =>
        new(
            repository,
            passwords,
            new FakeTokenService(),
            email,
            Options.Create(new AuthFlowOptions()),
            new FixedTimeProvider(Now),
            new LoginCommandValidator(),
            new RefreshTokenCommandValidator(),
            new LogoutCommandValidator(),
            new RequestPasswordRecoveryCommandValidator(),
            new VerifyPasswordRecoveryCommandValidator(),
            new ResetPasswordCommandValidator());

    private static AuthenticationUser ActiveUser(string passwordHash) => new()
    {
        Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        NombreCompleto = "Usuario Prueba",
        Correo = "user@example.com",
        PasswordHash = passwordHash,
        Estado = "ACTIVO",
        Activo = true,
        Roles = ["TECNICO_EVALUADOR"]
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public IssuedToken CreateAccessToken(AuthenticationUser user, DateTimeOffset now) => new("access-token", now.AddMinutes(15), Guid.NewGuid());
        public IssuedToken CreatePasswordResetToken(AuthenticationUser user, DateTimeOffset now) => new("reset-token", now.AddMinutes(10), Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public string? Otp { get; private set; }
        public bool ShouldFail { get; init; }
        public Task SendPasswordRecoveryOtpAsync(string recipient, string recipientName, string otp, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
        {
            if (ShouldFail) throw new EmailDeliveryException("SMTP no disponible.");
            Otp = otp;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuthenticationRepository : IAuthenticationRepository
    {
        public AuthenticationUser? User { get; init; }
        public OtpChallenge? Challenge { get; init; }
        public int FailedLogins { get; private set; }
        public bool SuccessfulLoginRecorded { get; private set; }
        public bool PreviousOtpsInvalidated { get; private set; }
        public int OtpInvalidations { get; private set; }
        public bool OtpConsumed { get; private set; }
        public string? StoredOtpHash { get; private set; }
        public string? StoredRefreshHash { get; private set; }
        public Guid? StoredProofId { get; private set; }
        public bool PasswordResetAccepted { get; init; } = true;
        public string? RevokedRefreshHash { get; private set; }

        public Task<AuthenticationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) => Task.FromResult(User);
        public Task RecordFailedLoginAsync(Guid userId, int maximumAttempts, DateTimeOffset blockedUntil, CancellationToken cancellationToken = default) { FailedLogins++; return Task.CompletedTask; }
        public Task RecordSuccessfulLoginAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default) { SuccessfulLoginRecorded = true; return Task.CompletedTask; }
        public Task InvalidateActiveOtpsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default) { PreviousOtpsInvalidated = true; OtpInvalidations++; return Task.CompletedTask; }
        public Task CreateOtpAsync(Guid userId, string otpHash, DateTimeOffset expiresAt, string? ipHash, CancellationToken cancellationToken = default) { StoredOtpHash = otpHash; return Task.CompletedTask; }
        public Task<OtpChallenge?> GetLatestActiveOtpAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Challenge);
        public Task RecordFailedOtpAttemptAsync(Guid otpId, int maximumAttempts, DateTimeOffset now, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ConsumeOtpAsync(Guid otpId, DateTimeOffset now, CancellationToken cancellationToken = default) { OtpConsumed = true; return Task.FromResult(true); }
        public Task StorePasswordResetProofAsync(Guid proofId, Guid userId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) { StoredProofId = proofId; return Task.CompletedTask; }
        public Task StoreRefreshTokenAsync(Guid userId, string tokenHash, Guid familyId, DateTimeOffset expiresAt, string? device, string? ipHash, CancellationToken cancellationToken = default) { StoredRefreshHash = tokenHash; return Task.CompletedTask; }
        public Task<AuthenticationUser?> RotateRefreshTokenAsync(string currentTokenHash, string replacementTokenHash, DateTimeOffset replacementExpiresAt, DateTimeOffset now, string? device, string? ipHash, CancellationToken cancellationToken = default) => Task.FromResult(User);
        public Task<bool> ConsumePasswordResetProofAndUpdatePasswordAsync(Guid proofId, Guid userId, string passwordHash, DateTimeOffset now, CancellationToken cancellationToken = default) => Task.FromResult(PasswordResetAccepted);
        public Task RevokeRefreshTokenAsync(Guid userId, string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default) { RevokedRefreshHash = tokenHash; return Task.CompletedTask; }
    }
}
