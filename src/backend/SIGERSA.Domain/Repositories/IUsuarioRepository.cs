using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Security;

namespace SIGERSA.Domain.Repositories;

public interface IUsuarioRepository
{
    Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task ActualizarNombreAsync(
        Guid id,
        string nombreCompleto,
        Guid modificadoPor,
        long versionFila,
        CancellationToken cancellationToken = default);

    Task<ManagedUsersPage> SearchManagedAsync(
        ManagedUsersQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleOption>> GetActiveRoleOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompanyOption>> GetActiveCompanyOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<bool> CanActivateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> PublicRegistrationExistsAsync(
        string normalizedEmail,
        string normalizedIdentification,
        CancellationToken cancellationToken = default);

    Task<Guid> CreatePublicRegistrationAsync(
        PublicUserRegistrationDraft draft,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateManagedAsync(
        ManagedUserDraft draft,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateManagedAsync(
        Guid id,
        ManagedUserDraft draft,
        Guid actorId,
        Guid? companyScope,
        CancellationToken cancellationToken = default);

    Task<bool> SetSuspendedAsync(
        Guid id,
        bool suspended,
        long versionFila,
        Guid actorId,
        Guid? companyScope,
        CancellationToken cancellationToken = default);
}
