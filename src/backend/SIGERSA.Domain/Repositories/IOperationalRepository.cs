using SIGERSA.Domain.Entities;

namespace SIGERSA.Domain.Repositories;

public interface IOperationalRepository
{
    Task<DashboardSnapshot> GetDashboardAsync(OperationalActorScope scope, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationRecord>> GetNotificationsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> MarkNotificationReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default);
    Task<SurveillancePage> SearchSurveillanceAsync(SurveillanceSearch search, CancellationToken cancellationToken = default);
    Task<SurveillanceOptions> GetSurveillanceOptionsAsync(bool canManage, CancellationToken cancellationToken = default);
    Task<Guid> CreateSurveillanceAsync(SurveillanceDraft draft, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> UpdateSurveillanceAsync(Guid id, SurveillanceDraft draft, Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OperationalOption>> GetPublicComplaintOptionsAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreatePublicComplaintAsync(PublicComplaintDraft draft, CancellationToken cancellationToken = default);
    Task<FindingsPage> SearchFindingsAsync(FindingSearch search, CancellationToken cancellationToken = default);
    Task<FindingDetail?> GetFindingAsync(Guid id, OperationalActorScope scope, CancellationToken cancellationToken = default);
    Task<FindingOptions> GetFindingOptionsAsync(Guid actorId, bool canCreate, CancellationToken cancellationToken = default);
    Task<Guid> CreateFindingAsync(FindingDraft draft, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> CloseFindingAsync(Guid id, long rowVersion, string reason, Guid actorId, CancellationToken cancellationToken = default);
    Task<HistoricalEvaluationsPage> SearchHistoryAsync(HistoricalEvaluationSearch search, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimelineEvent>> GetTimelineAsync(Guid evaluationId, OperationalActorScope scope, CancellationToken cancellationToken = default);
    Task<AuditEventsPage> SearchAuditAsync(AuditEventSearch search, CancellationToken cancellationToken = default);
    Task<ReportGenerationData?> GetReportDataAsync(Guid evaluationId, Guid actorId, CancellationToken cancellationToken = default);
    Task<ReportFileReference> SaveReportVersionAsync(Guid evaluationId, string bucketName, string supabasePath, long fileSize, string hash, bool isOfficial, Guid actorId, CancellationToken cancellationToken = default);
    Task<ReportFileReference?> GetReportFileAsync(Guid reportId, OperationalActorScope scope, CancellationToken cancellationToken = default);
}
