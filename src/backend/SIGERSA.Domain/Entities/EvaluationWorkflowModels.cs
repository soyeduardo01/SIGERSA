namespace SIGERSA.Domain.Entities;

public sealed record PublishedInspectionTemplate(
    Guid InspectionTemplateId,
    Guid RiskRuleVersionId,
    int Version,
    int ItemCount);

public sealed record EvaluationSession(Guid Id, string Number, long RowVersion);

public sealed record EvaluationFormItem(
    Guid Id,
    Guid? ParentId,
    int SourceItem,
    string Code,
    string Title,
    bool IsEvaluable,
    int Level,
    int Order);

public sealed record EvaluationAnswer(
    Guid Id,
    Guid EvaluationId,
    int SourceItem,
    string Rating,
    decimal? Score,
    long RowVersion);

public sealed record EvaluationCalculationInput(
    Guid EvaluationId,
    long RowVersion,
    IReadOnlyList<EvaluationFormItem> Items,
    IReadOnlyList<EvaluationAnswer> Answers);

public sealed record EvaluationCalculation(
    Guid EvaluationId,
    decimal? CompliancePercentage,
    decimal? ProductRisk,
    decimal? EstablishmentRisk,
    decimal? TotalRisk,
    string RiskLevel,
    string Frequency,
    long RowVersion);

public sealed record CreateEvaluationDraft(
    Guid CaseId,
    Guid EstablishmentId,
    Guid InspectionTemplateId,
    Guid EvaluatorId,
    Guid RiskRuleVersionId,
    DateTimeOffset? ScheduledStart,
    DateTimeOffset? ScheduledEnd);

public sealed record SaveEvaluationAnswerDraft(
    Guid EvaluationId,
    int SourceItem,
    string Rating,
    string? Observation,
    Guid IdempotencyKey,
    Guid DeviceId,
    long ClientSequence,
    DateTimeOffset ClientDate,
    long? BaseVersion);
