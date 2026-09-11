using SIGERSA.Domain.Entities;

namespace SIGERSA.Domain.Repositories;

public interface IInspectionRequestRepository
{
    Task<InspectionRequestsPage> SearchAsync(
        InspectionRequestSearch query,
        CancellationToken cancellationToken = default);

    Task<InspectionRequestOptions> GetOptionsAsync(
        Guid? companyScope,
        bool canManage,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(
        InspectionRequestDraft draft,
        Guid applicantId,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        Guid id,
        InspectionRequestDraft draft,
        Guid actorId,
        Guid? companyScope,
        CancellationToken cancellationToken = default);

    Task<bool> TransitionAsync(
        Guid id,
        long rowVersion,
        string targetStatus,
        Guid actorId,
        Guid? companyScope,
        CancellationToken cancellationToken = default);

    Task<bool> HasRequiredDocumentAsync(
        Guid id,
        Guid? companyScope,
        CancellationToken cancellationToken = default) => Task.FromResult(true);
}
