using FluentValidation;

namespace SIGERSA.Application.Requests;

public sealed record InspectionRequestActor(Guid UserId, string[] Roles, Guid? CompanyId);

public sealed record InspectionRequestInput(
    Guid? CompanyId,
    Guid? EstablishmentId,
    Guid InspectionReasonId,
    string? ReasonDetail,
    string? EstablishmentType,
    string? Observations,
    Guid IdempotencyKey,
    long? RowVersion,
    Guid? ApplicantUserId = null);

public sealed record InspectionRequestTransitionInput(long RowVersion);

public sealed class InspectionRequestInputValidator : AbstractValidator<InspectionRequestInput>
{
    public InspectionRequestInputValidator()
    {
        RuleFor(value => value.InspectionReasonId).NotEmpty();
        RuleFor(value => value.ReasonDetail).MaximumLength(2000);
        RuleFor(value => value.EstablishmentType).MaximumLength(100);
        RuleFor(value => value.Observations).MaximumLength(4000);
        RuleFor(value => value.RowVersion).GreaterThan(0).When(value => value.RowVersion.HasValue);
    }
}
