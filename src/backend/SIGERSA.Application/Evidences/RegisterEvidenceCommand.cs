using FluentValidation;

namespace SIGERSA.Application.Evidences;

public sealed record RegisterEvidenceCommand(
    Guid EvaluacionId,
    string BucketName,
    string SupabasePath,
    string MimeType,
    long FileSize,
    string Sha256Hash);

public sealed class RegisterEvidenceCommandValidator : AbstractValidator<RegisterEvidenceCommand>
{
    public RegisterEvidenceCommandValidator()
    {
        RuleFor(command => command.EvaluacionId).NotEmpty();
        RuleFor(command => command.BucketName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.SupabasePath)
            .NotEmpty()
            .MaximumLength(1024)
            .Must(path => !path.Contains("..", StringComparison.Ordinal) && !path.Contains('\\'))
            .WithMessage("La ruta de Supabase no puede contener segmentos relativos ni barras invertidas.");
        RuleFor(command => command.MimeType).NotEmpty().MaximumLength(150);
        RuleFor(command => command.FileSize).GreaterThan(0);
        RuleFor(command => command.Sha256Hash)
            .Matches("^[a-fA-F0-9]{64}$")
            .WithMessage("El hash debe ser un SHA-256 hexadecimal.");
    }
}
