using FluentValidation;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Security;

namespace SIGERSA.Application.Users;

public sealed class UserManagementService(
    IUsuarioRepository repository,
    IPasswordService passwordService,
    IValidator<UserManagementRequest> validator)
{
    private static readonly string[] CompanyAssignableRoles = ["ADMINISTRADOR_EMPRESA", "USUARIO_DELEGADO"];

    public Task<ManagedUsersPage> SearchAsync(
        string? search,
        string? role,
        string? status,
        int page,
        int pageSize,
        UserManagementActor actor,
        CancellationToken cancellationToken)
    {
        EnsureCanRead(actor);
        Guid? companyScope = CanReadGlobally(actor) ? null : RequiredCompanyScope(actor);
        return repository.SearchManagedAsync(
            new ManagedUsersQuery(
                NormalizeOptional(search)?.ToLowerInvariant(),
                NormalizeOptional(role)?.ToUpperInvariant(),
                NormalizeOptional(status)?.ToUpperInvariant(),
                Math.Max(page, 1),
                Math.Clamp(pageSize, 5, 100),
                companyScope),
            cancellationToken);
    }

    public async Task<UserManagementOptionsResponse> GetOptionsAsync(
        UserManagementActor actor,
        CancellationToken cancellationToken)
    {
        EnsureCanRead(actor);
        var roles = await repository.GetActiveRoleOptionsAsync(cancellationToken);
        var companies = await repository.GetActiveCompanyOptionsAsync(cancellationToken);
        var canManage = CanManage(actor);
        var allowedRoles = CanReadGlobally(actor)
            ? roles
            : roles.Where(role => CompanyAssignableRoles.Contains(role.Code, StringComparer.Ordinal)).ToArray();
        var allowedCompanies = CanReadGlobally(actor)
            ? companies
            : companies.Where(company => company.Id == RequiredCompanyScope(actor)).ToArray();
        return new UserManagementOptionsResponse(
            allowedRoles.Select(role => new UserRoleOptionResponse(role.Code, role.Name)).ToArray(),
            allowedCompanies.Select(company => new UserCompanyOptionResponse(company.Id, company.Name)).ToArray(),
            canManage);
    }

    public async Task<Guid> CreateAsync(
        UserManagementRequest request,
        UserManagementActor actor,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(actor);
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.TemporaryPassword))
        {
            throw new ArgumentException("La contraseña temporal es obligatoria al crear un usuario.");
        }

        var draft = BuildDraft(request, actor, passwordService.Hash(request.TemporaryPassword), null);
        return await repository.CreateManagedAsync(draft, actor.UserId, cancellationToken);
    }

    public async Task UpdateAsync(
        Guid id,
        UserManagementRequest request,
        UserManagementActor actor,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(actor);
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        if (request.VersionFila is null or <= 0)
        {
            throw new ArgumentException("La versión del usuario es obligatoria para actualizar.");
        }
        var passwordHash = string.IsNullOrWhiteSpace(request.TemporaryPassword)
            ? null
            : passwordService.Hash(request.TemporaryPassword);
        var draft = BuildDraft(request, actor, passwordHash, request.VersionFila);
        if (!await repository.UpdateManagedAsync(
                id,
                draft,
                actor.UserId,
                IsGlobalAdministrator(actor) ? null : RequiredCompanyScope(actor),
                cancellationToken))
        {
            throw new OptimisticConcurrencyException(id);
        }
    }

    public async Task SetSuspendedAsync(
        Guid id,
        bool suspended,
        long versionFila,
        UserManagementActor actor,
        CancellationToken cancellationToken)
    {
        EnsureCanManage(actor);
        if (id == actor.UserId) throw new InvalidOperationException("No puede bloquear su propia cuenta.");
        if (versionFila <= 0) throw new ArgumentException("La versión del usuario no es válida.");
        if (!await repository.SetSuspendedAsync(
                id,
                suspended,
                versionFila,
                actor.UserId,
                IsGlobalAdministrator(actor) ? null : RequiredCompanyScope(actor),
                cancellationToken))
        {
            throw new OptimisticConcurrencyException(id);
        }
    }

    private static ManagedUserDraft BuildDraft(
        UserManagementRequest request,
        UserManagementActor actor,
        string? passwordHash,
        long? versionFila)
    {
        var role = request.Rol.Trim().ToUpperInvariant();
        var companyId = request.EmpresaId;
        if (!IsGlobalAdministrator(actor))
        {
            if (!CompanyAssignableRoles.Contains(role, StringComparer.Ordinal))
                throw new ForbiddenException("No puede asignar roles internos o globales.");
            companyId = RequiredCompanyScope(actor);
        }
        if (CompanyAssignableRoles.Contains(role, StringComparer.Ordinal) && companyId is null)
            throw new ArgumentException("Los roles empresariales requieren una empresa.");

        return new ManagedUserDraft(
            request.NombreCompleto.Trim(),
            request.Correo.Trim().ToLowerInvariant(),
            request.TipoIdentificacion.Trim().ToUpperInvariant(),
            NormalizeIdentification(request.Identificacion),
            NormalizeOptional(request.Telefono),
            companyId,
            role,
            request.Estado.Trim().ToUpperInvariant(),
            passwordHash,
            versionFila);
    }

    private static bool IsGlobalAdministrator(UserManagementActor actor) =>
        actor.Roles.Contains("ADMINISTRADOR", StringComparer.Ordinal);

    private static bool CanManage(UserManagementActor actor) =>
        IsGlobalAdministrator(actor) || actor.Roles.Contains("ADMINISTRADOR_EMPRESA", StringComparer.Ordinal);

    private static bool CanReadGlobally(UserManagementActor actor) =>
        IsGlobalAdministrator(actor) || actor.Roles.Contains("COORDINADOR", StringComparer.Ordinal);

    private static void EnsureCanManage(UserManagementActor actor)
    {
        if (!CanManage(actor)) throw new ForbiddenException("No tiene permisos para administrar usuarios.");
    }

    private static void EnsureCanRead(UserManagementActor actor)
    {
        if (!CanManage(actor) && !actor.Roles.Contains("COORDINADOR", StringComparer.Ordinal))
            throw new ForbiddenException("No tiene permisos para consultar usuarios.");
    }

    private static Guid RequiredCompanyScope(UserManagementActor actor) =>
        actor.CompanyId ?? throw new ForbiddenException("El usuario no tiene un ámbito empresarial válido.");

    private static string NormalizeIdentification(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
