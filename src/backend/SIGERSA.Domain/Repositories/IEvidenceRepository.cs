using SIGERSA.Domain.Entities;

namespace SIGERSA.Domain.Repositories;

public interface IEvidenceRepository
{
    Task<EvidencesPage> SearchAsync(EvidenceSearch query, CancellationToken cancellationToken = default);
    Task<EvidenceStorageReference?> GetAuthorizedAsync(
        Guid evidenceId, Guid actorId, Guid? companyScope, bool globalScope, bool assignedOnly,
        CancellationToken cancellationToken = default);
    Task<bool> CanUploadAsync(Guid userId, Guid evaluationId, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(EvidenceRecord evidence, CancellationToken cancellationToken = default);
}
