using FluentValidation;

namespace SIGERSA.Application.Corrections;

public sealed record CorrectionActor(Guid UserId, string[] Roles, Guid? CompanyId);
public sealed record CorrectionInput(
    Guid EvaluationId, string ResponsibleType, Guid? AssignedToId,
    string CoordinatorObservation, DateTimeOffset DueAt, Guid IdempotencyKey,
    IReadOnlyList<CorrectionFieldInput>? Fields = null);
public sealed record CorrectionFieldInput(int SourceItem, string Reason);
public sealed record CorrectionTransitionInput(long RowVersion);

public sealed class CorrectionInputValidator : AbstractValidator<CorrectionInput>
{
    public static readonly TimeSpan MinimumResolutionTime = TimeSpan.FromMinutes(30);

    public CorrectionInputValidator()
    {
        RuleFor(value => value.EvaluationId).NotEmpty();
        RuleFor(value => value.ResponsibleType).Must(value => value is "TECNICO" or "EMPRESA");
        RuleFor(value => value.AssignedToId).NotEmpty().When(value => value.ResponsibleType == "TECNICO");
        RuleFor(value => value.CoordinatorObservation).NotEmpty().MaximumLength(4000);
        RuleFor(value => value.DueAt)
            .Must(dueAt => dueAt.ToUniversalTime() >= DateTimeOffset.UtcNow.Add(MinimumResolutionTime))
            .WithMessage("La fecha límite debe dejar al menos 30 minutos para gestionar la corrección.");
        RuleFor(value => value.IdempotencyKey).NotEmpty();
        RuleForEach(value => value.Fields).ChildRules(field =>
        {
            field.RuleFor(value => value.SourceItem).GreaterThan(0);
            field.RuleFor(value => value.Reason).NotEmpty().MaximumLength(2000);
        });
        RuleFor(value => value.Fields)
            .Must(fields => fields is null || fields.Select(field => field.SourceItem).Distinct().Count() == fields.Count)
            .WithMessage("No puede observar el mismo criterio más de una vez.");
    }
}
