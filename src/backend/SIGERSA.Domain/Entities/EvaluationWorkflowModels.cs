namespace SIGERSA.Domain.Entities;

public sealed record PublishedInspectionTemplate(
    Guid InspectionTemplateId,
    Guid RiskRuleVersionId,
    int Version,
    int ItemCount);

public sealed record EvaluationSession(Guid Id, string Number, long RowVersion);

public sealed record EvaluationSummary(
    Guid Id,
    string Number,
    Guid CaseId,
    string CaseNumber,
    Guid EstablishmentId,
    string EstablishmentName,
    Guid EvaluatorId,
    string EvaluatorName,
    string Status,
    DateTimeOffset? ScheduledStart,
    DateTimeOffset? ScheduledEnd,
    decimal? CompliancePercentage,
    decimal? TotalRisk,
    string? RiskLevel,
    int AnsweredItems,
    long RowVersion);

public sealed record EvaluationsPage(IReadOnlyList<EvaluationSummary> Items, int Page, int PageSize, int Total);
public sealed record EvaluationSearch(
    string? Search, string? Status, int Page, int PageSize, Guid ActorId,
    Guid? CompanyScope, bool GlobalScope, bool AssignedOnly);
public sealed record EvaluationOption(Guid Id, string Name, Guid? CompanyId = null);
public sealed record EvaluationCreateOptions(
    IReadOnlyList<EvaluationOption> Cases,
    IReadOnlyList<EvaluationOption> Establishments,
    IReadOnlyList<EvaluationOption> Templates,
    IReadOnlyList<EvaluationOption> RiskRules,
    IReadOnlyList<EvaluationOption> Evaluators,
    bool CanCreate);

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
    IReadOnlyList<EvaluationAnswer> Answers,
    decimal? MonthlyProduction,
    bool? HaccpImplemented,
    decimal? HaccpPercentage,
    bool? IsInabieSupplier,
    string? InabieDistributionCode,
    int MicrobiologicalRejectionsLastFiveYears,
    bool? MicrobiologicalSamplingPlan,
    string? SamplingApplicationCode);

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
    string? Comment,
    Guid IdempotencyKey,
    Guid DeviceId,
    long ClientSequence,
    DateTimeOffset ClientDate,
    long? BaseVersion);

public sealed record EvaluationTransitionDraft(
    string Action,
    long RowVersion,
    decimal? Latitude = null,
    decimal? Longitude = null,
    decimal? AccuracyMeters = null);
