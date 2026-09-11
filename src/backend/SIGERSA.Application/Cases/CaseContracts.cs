using FluentValidation;

namespace SIGERSA.Application.Cases;

public sealed record CaseActor(Guid UserId, string[] Roles, Guid? CompanyId);
public sealed record CaseInput(
    string Origin,
    Guid SourceId,
    short Priority,
    Guid? ResponsibleId,
    string? AnalysisDecision,
    string? DecisionReason,
    Guid IdempotencyKey,
    long? RowVersion);
public sealed record CloseCaseInput(long RowVersion, string Reason);

public sealed class CaseInputValidator : AbstractValidator<CaseInput>
{
    public CaseInputValidator()
    {
        RuleFor(value => value.Origin)
            .Must(value => value is "SOLICITUD_EMPRESA" or "PROGRAMACION" or "ALERTA_LAPCH" or "DENUNCIA")
            .WithMessage("El origen del caso no es válido.");
        RuleFor(value => value.SourceId).NotEmpty();
        RuleFor(value => value.Priority).InclusiveBetween((short)1, (short)5);
        RuleFor(value => value.AnalysisDecision)
            .Must(value => value is null or "PROCEDE" or "NO_PROCEDE" or "REQUIERE_INFORMACION")
            .WithMessage("La decisión de análisis no es válida.");
        RuleFor(value => value.DecisionReason).MaximumLength(2000);
        RuleFor(value => value.RowVersion).GreaterThan(0).When(value => value.RowVersion.HasValue);
    }
}
