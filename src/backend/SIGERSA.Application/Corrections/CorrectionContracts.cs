using FluentValidation;

namespace SIGERSA.Application.Corrections;

public sealed record CorrectionActor(Guid UserId, string[] Roles, Guid? CompanyId);
public sealed record CorrectionInput(
    Guid EvaluationId, string ResponsibleType, Guid? AssignedToId,
    string CoordinatorObservation, DateTimeOffset DueAt, Guid IdempotencyKey);
public sealed record CorrectionTransitionInput(long RowVersion);

public sealed class CorrectionInputValidator : AbstractValidator<CorrectionInput>
{
    public CorrectionInputValidator()
    {
        RuleFor(value => value.EvaluationId).NotEmpty();
        RuleFor(value => value.ResponsibleType).Must(value => value is "TECNICO" or "EMPRESA");
        RuleFor(value => value.AssignedToId).NotEmpty().When(value => value.ResponsibleType == "TECNICO");
        RuleFor(value => value.CoordinatorObservation).NotEmpty().MaximumLength(4000);
        RuleFor(value => value.DueAt).GreaterThan(DateTimeOffset.UtcNow);
        RuleFor(value => value.IdempotencyKey).NotEmpty();
    }
}
