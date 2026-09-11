namespace SIGERSA.Domain.Entities;

public sealed record ScheduleRecord(
    Guid Id, Guid CaseId, string CaseNumber, string CompanyName, string EstablishmentName,
    string CaseOrigin, string? RequestNumber, string? Location,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, short Priority, string Status,
    string? Observations, string? ChangeReason, Guid[] EvaluatorIds, string[] EvaluatorNames, long RowVersion);
public sealed record SchedulesPage(IReadOnlyList<ScheduleRecord> Items, int Page, int PageSize, int Total);
public sealed record ScheduleSearch(
    string? Search, string? Status, int Page, int PageSize, Guid ActorId, bool AssignedOnly);
public sealed record ScheduleDraft(
    Guid IdempotencyKey, Guid CaseId, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    short Priority, string? Observations, string? ChangeReason, Guid[] EvaluatorIds, long? RowVersion);
public sealed record ScheduleOption(Guid Id, string Name);
public sealed record ScheduleOptions(
    IReadOnlyList<ScheduleOption> Cases,
    IReadOnlyList<ScheduleOption> Evaluators,
    bool CanManage);
