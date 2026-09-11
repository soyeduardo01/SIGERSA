namespace SIGERSA.Domain.Entities;

public sealed record CaseRecord(
    Guid Id,
    string Number,
    Guid? RequestId,
    Guid? AlertId,
    Guid? ComplaintId,
    Guid CompanyId,
    string CompanyName,
    Guid EstablishmentId,
    string EstablishmentName,
    string Origin,
    string Status,
    short Priority,
    Guid? ResponsibleId,
    string? ResponsibleName,
    string? AnalysisDecision,
    string? DecisionReason,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    long RowVersion);

public sealed record CasesPage(IReadOnlyList<CaseRecord> Items, int Page, int PageSize, int Total);

public sealed record CaseSearch(
    string? Search,
    string? Status,
    int Page,
    int PageSize,
    Guid ActorId,
    Guid? CompanyScope,
    bool GlobalScope,
    bool AssignedOnly);

public sealed record CaseDraft(
    Guid IdempotencyKey,
    string Origin,
    Guid SourceId,
    short Priority,
    Guid? ResponsibleId,
    string? AnalysisDecision,
    string? DecisionReason,
    long? RowVersion);

public sealed record CaseOption(Guid Id, string Name, Guid? CompanyId = null);
public sealed record CaseSourceOption(Guid Id, string Kind, string Name, Guid CompanyId, Guid EstablishmentId);
public sealed record CaseOptions(
    IReadOnlyList<CaseSourceOption> Sources,
    IReadOnlyList<CaseOption> ResponsibleUsers,
    bool CanManage);
