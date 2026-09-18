using FluentValidation;
using SIGERSA.Application.Common;

namespace SIGERSA.Application.Profiles;

public sealed record ProfileResponse(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    string Status,
    DateTimeOffset? LastAccessAt,
    bool MfaEnabled,
    long RowVersion);

public sealed record BeginMfaEnrollmentRequest(string CurrentPassword);

public sealed record MfaEnrollmentBootstrap(string Email);

public sealed record CompleteMfaEnrollmentRequest(Guid FactorId, string SupabaseAccessToken);

public sealed record DisableMfaRequest(
    string CurrentPassword,
    Guid FactorId,
    string SupabaseAccessToken);

public sealed record UpdateProfileRequest(
    string FullName,
    string Email,
    string? Phone,
    long RowVersion);

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword,
    long RowVersion);

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(request => request.FullName).NotEmpty().MaximumLength(250);
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.Phone).MaximumLength(40).Must(DominicanPhone.IsValid)
            .WithMessage("El teléfono debe tener 10 dígitos y comenzar con 809, 829 o 849.");
        RuleFor(request => request.RowVersion).GreaterThan(0);
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(request => request.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("La nueva contraseña debe contener una mayúscula.")
            .Matches("[a-z]").WithMessage("La nueva contraseña debe contener una minúscula.")
            .Matches("[0-9]").WithMessage("La nueva contraseña debe contener un número.")
            .Matches("[^a-zA-Z0-9]").WithMessage("La nueva contraseña debe contener un símbolo.");
        RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.NewPassword)
            .WithMessage("La confirmación no coincide con la nueva contraseña.");
        RuleFor(request => request.RowVersion).GreaterThan(0);
    }
}
