namespace SIGERSA.Domain.Security;

public sealed class AuthenticationUser
{
    public Guid Id { get; init; }

    public Guid? EmpresaId { get; init; }

    public required string NombreCompleto { get; init; }

    public required string Correo { get; init; }

    public required string PasswordHash { get; init; }

    public required string Estado { get; init; }

    public bool Activo { get; init; }

    public int FallosAcceso { get; init; }

    public DateTimeOffset? BloqueadoHasta { get; init; }

    public string[] Roles { get; init; } = [];
}

public sealed record OtpChallenge(
    Guid Id,
    Guid UsuarioId,
    string OtpHash,
    DateTimeOffset ExpiraEn,
    int Intentos,
    string Estado);

public sealed record RefreshTokenOwner(
    AuthenticationUser User,
    Guid TokenId,
    Guid FamilyId,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt);

public sealed record IssuedToken(string Value, DateTimeOffset ExpiresAt, Guid TokenId);
