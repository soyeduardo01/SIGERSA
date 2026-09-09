using SIGERSA.Domain.Entities;

namespace SIGERSA.Domain.Repositories;

public interface IEvaluationWorkflowRepository
{
    Task<PublishedInspectionTemplate> PublishAllItemsAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<EvaluationSession> CreateEvaluationAsync(CreateEvaluationDraft draft, Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationFormItem>> GetFormAsync(Guid evaluationId, Guid actorId, CancellationToken cancellationToken = default);
    Task<EvaluationAnswer> SaveAnswerAsync(SaveEvaluationAnswerDraft draft, Guid actorId, CancellationToken cancellationToken = default);
    Task<EvaluationCalculationInput> GetCalculationInputAsync(Guid evaluationId, Guid actorId, CancellationToken cancellationToken = default);
    Task<long> SaveCalculationAsync(EvaluationCalculation calculation, string snapshotJson, string snapshotHash, long expectedVersion, Guid actorId, CancellationToken cancellationToken = default);
}
