using SIGERSA.Domain.Common;

namespace SIGERSA.Domain.Entities;

public sealed class Usuario : AuditableEntity
{
    public required string Correo { get; init; }

    public required string NombreCompleto { get; init; }

    public bool Activo { get; init; }
}
