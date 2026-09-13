namespace SIGERSA.Domain.Entities;

public sealed record CorrectionRecord(
    Guid Id, Guid EvaluationId, string EvaluationNumber, string EstablishmentName,
    int RevisionNumber, string ResponsibleType, Guid? AssignedToId, string? AssignedToName,
    string CoordinatorObservation, string Status, DateTimeOffset DueAt,
    DateTimeOffset RequestedAt, DateTimeOffset? SubmittedAt, DateTimeOffset? ResolvedAt,
    IReadOnlyList<CorrectionField> Fields,
    long RowVersion);
public sealed record CorrectionField(int SourceItem, string ItemTitle, string Reason, string Status);
public sealed record CorrectionsPage(IReadOnlyList<CorrectionRecord> Items, int Page, int PageSize, int Total);
public sealed record CorrectionSearch(
    string? Search, string? Status, int Page, int PageSize, Guid ActorId,
    Guid? CompanyScope, bool GlobalScope, bool AssignedOnly, string? ReviewScope = null);
public sealed record CorrectionDraft(
    Guid IdempotencyKey, Guid EvaluationId, string ResponsibleType,
    Guid? AssignedToId, string CoordinatorObservation, DateTimeOffset DueAt,
    IReadOnlyList<CorrectionFieldDraft> Fields);
public sealed record CorrectionFieldDraft(int SourceItem, string Reason);
public sealed record CorrectionOption(Guid Id, string Name);
public sealed record CorrectionOptions(
    IReadOnlyList<CorrectionOption> Evaluations,
    IReadOnlyList<CorrectionOption> Technicians,
    bool CanCreate);
