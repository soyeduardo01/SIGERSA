namespace SIGERSA.Domain.Entities;

public sealed record InspectionRequestRecord(
    Guid Id,
    string? Number,
    Guid CompanyId,
    string CompanyName,
    Guid? EstablishmentId,
    string? EstablishmentName,
    Guid ApplicantId,
    string ApplicantName,
    Guid InspectionReasonId,
    string InspectionReasonName,
    string? ReasonDetail,
    string? EstablishmentType,
    string? Observations,
    int DocumentCount,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? CancelledAt,
    long RowVersion);

public sealed record InspectionRequestsPage(
    IReadOnlyList<InspectionRequestRecord> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record InspectionRequestSearch(
    string? Search,
    string? Status,
    int Page,
    int PageSize,
    Guid ActorId,
    Guid? CompanyScope,
    bool GlobalScope,
    bool AssignedOnly);

public sealed record InspectionRequestDraft(
    Guid IdempotencyKey,
    Guid CompanyId,
    Guid? EstablishmentId,
    Guid InspectionReasonId,
    string? ReasonDetail,
    string? EstablishmentType,
    string? Observations,
    long? RowVersion);

public sealed record InspectionRequestOption(Guid Id, string Name, Guid? CompanyId = null);

public sealed record InspectionRequestOptions(
    IReadOnlyList<InspectionRequestOption> Companies,
    IReadOnlyList<InspectionRequestOption> Establishments,
    IReadOnlyList<InspectionRequestOption> Reasons,
    IReadOnlyList<string> EstablishmentTypes,
    bool CanManage,
    IReadOnlyList<InspectionRequestOption>? Delegates = null);
