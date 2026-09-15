using SIGERSA.Domain.Entities;

namespace SIGERSA.Domain.Repositories;

public interface IEvaluationWorkflowRepository
{
    Task<EvaluationsPage> SearchAsync(EvaluationSearch query, CancellationToken cancellationToken = default);
    Task<EvaluationCreateOptions> GetOptionsAsync(bool canCreate, CancellationToken cancellationToken = default);
    Task<PublishedInspectionTemplate> PublishAllItemsAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<EvaluationSession> CreateEvaluationAsync(CreateEvaluationDraft draft, Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationFormItem>> GetFormAsync(Guid evaluationId, Guid actorId, CancellationToken cancellationToken = default);
    Task<EvaluationWorkspaceData> GetWorkspaceAsync(Guid evaluationId, Guid actorId, CancellationToken cancellationToken = default);
    Task<EvaluationInspectionContext> GetInspectionContextAsync(Guid evaluationId, Guid actorId, CancellationToken cancellationToken = default);
    Task<EvaluationSupplement> SaveSupplementAsync(Guid evaluationId, SaveEvaluationSupplementDraft draft, Guid actorId, CancellationToken cancellationToken = default);
    Task<EvaluationAnswer> SaveAnswerAsync(SaveEvaluationAnswerDraft draft, Guid actorId, CancellationToken cancellationToken = default);
    Task<EvaluationCalculationInput> GetCalculationInputAsync(Guid evaluationId, Guid actorId, CancellationToken cancellationToken = default);
    Task<long> SaveCalculationAsync(EvaluationCalculation calculation, decimal obtainedScore, decimal applicableScore, string snapshotJson, string snapshotHash, long expectedVersion, Guid actorId, CancellationToken cancellationToken = default);
    Task<long?> TransitionAsync(Guid evaluationId, EvaluationTransitionDraft transition, Guid actorId, CancellationToken cancellationToken = default);
}
