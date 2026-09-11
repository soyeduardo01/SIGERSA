using SIGERSA.Domain.Entities;

namespace SIGERSA.Domain.Repositories;

public interface IEstablishmentRepository
{
    Task<EstablishmentsPage> SearchAsync(EstablishmentSearch search, CancellationToken cancellationToken = default);
    Task<EstablishmentDetails?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EstablishmentOptions> GetOptionsAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(EstablishmentDraft draft, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Guid id, EstablishmentDraft draft, Guid actorId, CancellationToken cancellationToken = default);
}
