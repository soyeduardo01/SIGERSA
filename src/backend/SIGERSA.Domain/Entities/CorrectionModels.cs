namespace SIGERSA.Domain.Entities;

public sealed record CorrectionRecord(
    Guid Id, Guid EvaluationId, string EvaluationNumber, string EstablishmentName,
    int RevisionNumber, string ResponsibleType, Guid? AssignedToId, string? AssignedToName,
    string CoordinatorObservation, string Status, DateTimeOffset DueAt,
    DateTimeOffset RequestedAt, DateTimeOffset? SubmittedAt, DateTimeOffset? ResolvedAt,
    long RowVersion);
public sealed record CorrectionsPage(IReadOnlyList<CorrectionRecord> Items, int Page, int PageSize, int Total);
public sealed record CorrectionSearch(
    string? Search, string? Status, int Page, int PageSize, Guid ActorId,
    Guid? CompanyScope, bool GlobalScope, bool AssignedOnly);
public sealed record CorrectionDraft(
    Guid IdempotencyKey, Guid EvaluationId, string ResponsibleType,
    Guid? AssignedToId, string CoordinatorObservation, DateTimeOffset DueAt);
public sealed record CorrectionOption(Guid Id, string Name);
public sealed record CorrectionOptions(
    IReadOnlyList<CorrectionOption> Evaluations,
    IReadOnlyList<CorrectionOption> Technicians,
    bool CanCreate);
