using SIGERSA.Domain.Entities;

namespace SIGERSA.Domain.Repositories;

public interface ICaseRepository
{
    Task<CasesPage> SearchAsync(CaseSearch query, CancellationToken cancellationToken = default);
    Task<CaseOptions> GetOptionsAsync(bool canManage, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CaseDraft draft, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Guid id, CaseDraft draft, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> CloseAsync(
        Guid id,
        long rowVersion,
        string reason,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
