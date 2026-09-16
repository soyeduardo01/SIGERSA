namespace SIGERSA.Domain.Entities;

public sealed record OperationalActorScope(
    Guid UserId,
    Guid? CompanyId,
    bool GlobalScope,
    bool AssignedOnly,
    bool OwnerOnly = false);

public sealed record DashboardSnapshot(
    int ActiveCases,
    int PendingEvaluations,
    int CriticalAlerts,
    decimal? AverageCompliance,
    int MyRequests,
    int UnreadNotifications,
    int ScheduledEvaluations,
    int OpenComplaints,
    int PendingAssignments,
    int PendingReports,
    IReadOnlyList<DashboardEvaluation> RecentEvaluations,
    IReadOnlyList<DashboardRiskSlice> RiskDistribution,
    IReadOnlyList<DashboardUpcomingSchedule> UpcomingSchedules);

public sealed record DashboardEvaluation(
    Guid Id,
    string Number,
    string EstablishmentName,
    string Status,
    decimal? CompliancePercentage,
    string? RiskLevel,
    DateTimeOffset UpdatedAt);

public sealed record DashboardRiskSlice(string Level, int Total);

public sealed record DashboardUpcomingSchedule(
    Guid Id,
    string CaseNumber,
    string EstablishmentName,
    DateTimeOffset StartsAt,
    string Status);

public sealed record NotificationRecord(
    Guid Id,
    string Type,
    string Title,
    string Message,
    string? ResourceType,
    Guid? ResourceId,
    DateTimeOffset ScheduledFor,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record SurveillanceRecord(
    Guid Id,
    string Kind,
    string Number,
    DateTimeOffset OccurredAt,
    Guid? CompanyId,
    string? CompanyName,
    Guid? EstablishmentId,
    string? EstablishmentName,
    string Subject,
    string Description,
    short Priority,
    string? Channel,
    bool IsAnonymous,
    bool IsConfidential,
    string? Result,
    bool HasCase,
    long RowVersion);

public sealed record SurveillancePage(
    IReadOnlyList<SurveillanceRecord> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record SurveillanceSearch(
    string? Search,
    string? Kind,
    string? Result,
    int Page,
    int PageSize,
    OperationalActorScope Scope);

public sealed record SurveillanceDraft(
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

public sealed record OperationalOption(Guid Id, string Name, Guid? CompanyId = null);

public sealed record SurveillanceOptions(
    IReadOnlyList<OperationalOption> Companies,
    IReadOnlyList<OperationalOption> Establishments,
    bool CanManage);

public sealed record PublicComplaintDraft(
    Guid EstablishmentId,
    string ComplaintType,
    string Description,
    bool IsConfidential);

public sealed record FindingRecord(
    Guid Id,
    Guid EvaluationId,
    string EvaluationNumber,
    string EstablishmentName,
    int SourceItem,
    string ItemTitle,
    string Criticality,
    string Code,
    string Description,
    string Status,
    DateTimeOffset DetectedAt,
    DateTimeOffset? ClosedAt,
    long RowVersion);

public sealed record FindingsPage(
    IReadOnlyList<FindingRecord> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record FindingSearch(
    string? Search,
    string? Status,
    int Page,
    int PageSize,
    OperationalActorScope Scope);

public sealed record FindingDraft(
    Guid EvaluationId,
    int SourceItem,
    Guid CriticalityId,
    string Description);

public sealed record FindingOptions(
    IReadOnlyList<OperationalOption> Evaluations,
    IReadOnlyList<OperationalOption> Criticalities,
    bool CanCreate);

public sealed record HistoricalEvaluation(
    Guid Id,
    string Number,
    string CaseNumber,
    string CompanyName,
    string EstablishmentName,
    string EvaluatorName,
    string Status,
    decimal? CompliancePercentage,
    decimal? TotalRisk,
    string? RiskLevel,
    string? Frequency,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    Guid? ReportId,
    string? ReportNumber,
    string? ReportStatus,
    bool HasOfficialReport);

public sealed record HistoricalEvaluationsPage(
    IReadOnlyList<HistoricalEvaluation> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record HistoricalEvaluationSearch(
    string? Search,
    string? Status,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize,
    OperationalActorScope Scope);

public sealed record TimelineEvent(
    DateTimeOffset OccurredAt,
    string EventType,
    string Title,
    string? Detail,
    string? ActorName);

public sealed record AuditEventRecord(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Action,
    string ResourceType,
    Guid? ResourceId,
    string Result,
    string? ActorName,
    string? Reason,
    Guid? CorrelationId);

public sealed record AuditEventsPage(
    IReadOnlyList<AuditEventRecord> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record AuditEventSearch(
    string? Search,
    string? Result,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize);

public sealed record ReportGenerationData(
    Guid EvaluationId,
    string EvaluationNumber,
    string CaseNumber,
    string CompanyName,
    string EstablishmentName,
    string Address,
    string EvaluatorName,
    string Status,
    decimal? CompliancePercentage,
    decimal? ProductRisk,
    decimal? EstablishmentRisk,
    decimal? TotalRisk,
    string? RiskLevel,
    string? Frequency,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<ReportFinding> Findings,
    IReadOnlyList<ReportEvidence> Evidences);

public sealed record ReportFinding(string Code, string Criticality, string Description, string Status);
public sealed record ReportEvidence(
    string Name,
    string Type,
    string MimeType,
    string? BucketName = null,
    string? SupabasePath = null);

public sealed record ReportFileReference(
    Guid ReportId,
    string ReportNumber,
    int Version,
    string BucketName,
    string SupabasePath,
    string MimeType,
    string FileName,
    bool IsOfficial);
