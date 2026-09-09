using SIGERSA.Domain.Entities;

namespace SIGERSA.Domain.Repositories;

public interface IEvidenceRepository
{
    Task<bool> CanUploadAsync(Guid userId, Guid evaluationId, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(EvidenceRecord evidence, CancellationToken cancellationToken = default);
}
