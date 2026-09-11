using FluentValidation;

namespace SIGERSA.Application.Authentication;

public sealed record LoginCommand(string Email, string Password, string? Device, string? IpHash);

public sealed record VerifyTwoFactorCommand(string Email, string Otp, string? Device, string? IpHash);

public sealed record RefreshTokenCommand(string RefreshToken, string? Device, string? IpHash);

public sealed record LogoutCommand(Guid UserId, string RefreshToken);

public sealed record RequestPasswordRecoveryCommand(string Email, string? IpHash);

public sealed record VerifyPasswordRecoveryCommand(string Email, string Otp);

public sealed record ResetPasswordCommand(Guid UserId, Guid ProofId, string NewPassword);

public sealed record AuthTokensResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    string[] Roles);

public sealed record LoginResult(
    bool RequiresTwoFactor,
    DateTimeOffset? ExpiresAt,
    AuthTokensResponse? Session);

public sealed record PasswordRecoveryTokenResponse(string ResetToken, DateTimeOffset ExpiresAt);

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(command => command.Password).NotEmpty().MaximumLength(1024);
        RuleFor(command => command.Device).MaximumLength(250);
        RuleFor(command => command.IpHash).MaximumLength(128);
    }
}

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(command => command.RefreshToken).NotEmpty().MaximumLength(2048);
        RuleFor(command => command.Device).MaximumLength(250);
        RuleFor(command => command.IpHash).MaximumLength(128);
    }
}

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.RefreshToken).NotEmpty().MaximumLength(2048);
    }
}

public sealed class RequestPasswordRecoveryCommandValidator : AbstractValidator<RequestPasswordRecoveryCommand>
{
    public RequestPasswordRecoveryCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(command => command.IpHash).MaximumLength(128);
    }
}

public sealed class VerifyPasswordRecoveryCommandValidator : AbstractValidator<VerifyPasswordRecoveryCommand>
{
    public VerifyPasswordRecoveryCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(command => command.Otp).Matches("^[0-9]{6}$");
    }
}

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.ProofId).NotEmpty();
        RuleFor(command => command.NewPassword)
            .MinimumLength(8)
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("La contraseña debe contener una mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe contener una minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe contener un número.")
            .Matches("[^a-zA-Z0-9]").WithMessage("La contraseña debe contener un símbolo.");
    }
}
