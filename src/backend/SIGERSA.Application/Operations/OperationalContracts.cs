using FluentValidation;

namespace SIGERSA.Application.Operations;

public sealed record OperationalActor(Guid UserId, string[] Roles, Guid? CompanyId);

public sealed record SurveillanceRequest(
    string Kind,
    DateTimeOffset OccurredAt,
    Guid? CompanyId,
    Guid? EstablishmentId,
    string Subject,
    string Description,
    short Priority,
    string? Channel,
    bool IsAnonymous,
    bool IsConfidential,
    string? Result,
    long? RowVersion);

public sealed record FindingRequest(
    Guid EvaluationId,
    int SourceItem,
    Guid CriticalityId,
    string Description);

public sealed record CloseFindingRequest(long RowVersion, string Reason);

public sealed record UpdateFindingStatusRequest(long RowVersion, string Status, string? Reason);

public sealed class SurveillanceRequestValidator : AbstractValidator<SurveillanceRequest>
{
    private static readonly string[] AlertResults = ["PROCEDE_EVALUACION", "NO_PROCEDE", "REQUIERE_INFORMACION"];
    private static readonly string[] ComplaintResults = ["PROCEDE", "NO_PROCEDE", "REMITIDA_OTRO_PROCESO", "REQUIERE_INFORMACION"];

    public SurveillanceRequestValidator()
    {
        RuleFor(value => value.Kind).Must(value => value is "ALERTA_LAPCH" or "DENUNCIA");
        RuleFor(value => value.OccurredAt).NotEmpty();
        RuleFor(value => value.Subject).NotEmpty().MaximumLength(300);
        RuleFor(value => value.Description).NotEmpty().MaximumLength(5000);
        RuleFor(value => value.Priority).InclusiveBetween((short)1, (short)5);
        RuleFor(value => value.Channel).MaximumLength(50);
        RuleFor(value => value.EstablishmentId).NotEmpty().When(value => value.Kind == "DENUNCIA");
        RuleFor(value => value.Channel).NotEmpty().When(value => value.Kind == "DENUNCIA");
        RuleFor(value => value.Result).Must((request, result) =>
                string.IsNullOrWhiteSpace(result) ||
                (request.Kind == "ALERTA_LAPCH" ? AlertResults : ComplaintResults)
                    .Contains(result.Trim().ToUpperInvariant(), StringComparer.Ordinal))
            .WithMessage("El resultado no es válido para el tipo de registro.");
        RuleFor(value => value.RowVersion).GreaterThan(0).When(value => value.RowVersion.HasValue);
    }
}

public sealed class FindingRequestValidator : AbstractValidator<FindingRequest>
{
    public FindingRequestValidator()
    {
        RuleFor(value => value.EvaluationId).NotEmpty();
        RuleFor(value => value.SourceItem).GreaterThan(0);
        RuleFor(value => value.CriticalityId).NotEmpty();
        RuleFor(value => value.Description).NotEmpty().MaximumLength(4000);
    }
}
