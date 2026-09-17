using FluentValidation;

namespace SIGERSA.Application.Users;

public sealed record UserManagementActor(Guid UserId, string[] Roles, Guid? CompanyId);

public sealed record UserManagementRequest(
    string NombreCompleto,
    string Correo,
    string TipoIdentificacion,
    string Identificacion,
    string? Telefono,
    Guid? EmpresaId,
    string Rol,
    string Estado,
    string? TemporaryPassword,
    long? VersionFila);

public sealed record UserManagementOptionsResponse(
    IReadOnlyList<UserRoleOptionResponse> Roles,
    IReadOnlyList<UserCompanyOptionResponse> Companies,
    bool CanManage);

public sealed record UserRoleOptionResponse(string Code, string Name);

public sealed record UserCompanyOptionResponse(Guid Id, string Name);

public sealed class UserManagementRequestValidator : AbstractValidator<UserManagementRequest>
{
    private static readonly string[] AllowedRoles =
    [
        "ADMINISTRADOR",
        "ADMINISTRADOR_EMPRESA",
        "USUARIO_DELEGADO",
        "COORDINADOR",
        "TECNICO_EVALUADOR",
        "LABORATORISTA"
    ];

    public UserManagementRequestValidator()
    {
        RuleFor(request => request.NombreCompleto).NotEmpty().MaximumLength(250);
        RuleFor(request => request.Correo).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.TipoIdentificacion).NotEmpty().MaximumLength(30);
        RuleFor(request => request.Identificacion).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Telefono).MaximumLength(40);
        RuleFor(request => request.Rol).Must(role => AllowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            .WithMessage("El rol indicado no es válido.");
        RuleFor(request => request.Estado).Must(status => status is
                "PENDIENTE_VALIDACION" or "ACTIVO" or "RECHAZADO" or "SUSPENDIDO")
            .WithMessage("El estado de validación del usuario no es válido.");
        RuleFor(request => request.TemporaryPassword)
            .MinimumLength(8)
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("La contraseña temporal debe contener una mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña temporal debe contener una minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña temporal debe contener un número.")
            .Matches("[^a-zA-Z0-9]").WithMessage("La contraseña temporal debe contener un símbolo.")
            .When(request => !string.IsNullOrWhiteSpace(request.TemporaryPassword));
    }
}
