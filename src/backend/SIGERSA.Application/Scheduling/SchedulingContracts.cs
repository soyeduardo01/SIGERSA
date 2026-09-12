using FluentValidation;

namespace SIGERSA.Application.Scheduling;

public sealed record SchedulingActor(Guid UserId, string[] Roles);
public sealed record ScheduleInput(
    Guid CaseId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, short Priority,
    string? Observations, string? ChangeReason, Guid[] EvaluatorIds, Guid IdempotencyKey, long? RowVersion);
public sealed record CancelScheduleInput(long RowVersion, string Reason);

public sealed class ScheduleInputValidator : AbstractValidator<ScheduleInput>
{
    public ScheduleInputValidator()
    {
        RuleFor(value => value.CaseId).NotEmpty();
        RuleFor(value => value.EndsAt)
            .Must((input, endsAt) => endsAt.ToUniversalTime() > input.StartsAt.ToUniversalTime())
            .WithMessage("La fecha y hora de fin debe ser posterior a la fecha y hora de inicio.");
        RuleFor(value => value.Priority).InclusiveBetween((short)1, (short)5);
        RuleFor(value => value.EvaluatorIds).NotEmpty().Must(ids => ids.Distinct().Count() == ids.Length)
            .WithMessage("Debe seleccionar al menos un técnico sin duplicados.");
        RuleFor(value => value.Observations).MaximumLength(4000);
        RuleFor(value => value.ChangeReason).MaximumLength(2000);
        RuleFor(value => value.RowVersion).GreaterThan(0).When(value => value.RowVersion.HasValue);
    }
}
