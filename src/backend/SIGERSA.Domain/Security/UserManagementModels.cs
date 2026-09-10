namespace SIGERSA.Domain.Security;

public sealed record ManagedUser(
    Guid Id,
    string NombreCompleto,
    string Correo,
    string TipoIdentificacion,
    string Identificacion,
    string? Telefono,
    string[] Roles,
    Guid? EmpresaId,
    string? EmpresaNombre,
    string Estado,
    bool Activo,
    long VersionFila);

public sealed record ManagedUsersPage(
    IReadOnlyList<ManagedUser> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record ManagedUsersQuery(
    string? Search,
    string? Role,
    string? Status,
    int Page,
    int PageSize,
    Guid? CompanyScope);

public sealed record ManagedUserDraft(
    string NombreCompleto,
    string Correo,
    string TipoIdentificacion,
    string IdentificacionNormalizada,
    string? Telefono,
    Guid? EmpresaId,
    string Rol,
    string Estado,
    string? PasswordHash,
    long? VersionFila);

public sealed record RoleOption(string Code, string Name);

public sealed record CompanyOption(Guid Id, string Name);
