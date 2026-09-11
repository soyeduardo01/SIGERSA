using SIGERSA.Domain.Entities;

namespace SIGERSA.Domain.Repositories;

public interface ISupportingDocumentRepository
{
    Task<bool> CanAttachToUserAsync(Guid targetUserId, Guid? companyScope, bool globalScope,
        CancellationToken cancellationToken = default);
    Task<bool> CanAttachToRequestAsync(Guid requestId, Guid actorId, Guid? companyScope, bool globalScope,
        CancellationToken cancellationToken = default);
    Task<Guid> SaveUserAuthorizationAsync(Guid userId, SupportingDocument document, Guid actorId,
        CancellationToken cancellationToken = default);
    Task<Guid> SaveRequestDocumentAsync(Guid requestId, string documentType, bool required,
        SupportingDocument document, Guid actorId, CancellationToken cancellationToken = default);
    Task<SupportingDocumentReference?> GetUserAuthorizationAsync(
        Guid userId, Guid? companyScope, bool globalScope,
        CancellationToken cancellationToken = default);
}
