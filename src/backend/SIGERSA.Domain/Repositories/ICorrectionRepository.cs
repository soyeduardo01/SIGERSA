using SIGERSA.Domain.Entities;

namespace SIGERSA.Domain.Repositories;

public interface ICorrectionRepository
{
    Task<CorrectionsPage> SearchAsync(CorrectionSearch query, CancellationToken cancellationToken = default);
    Task<CorrectionOptions> GetOptionsAsync(bool canCreate, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CorrectionDraft draft, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> SubmitAsync(
        Guid id, long rowVersion, Guid actorId, Guid? companyScope, bool globalScope, bool assignedOnly,
        CancellationToken cancellationToken = default);
    Task<bool> ResolveAsync(
        Guid id, long rowVersion, string targetStatus, Guid actorId,
        CancellationToken cancellationToken = default);
}
