using FluentValidation;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Security;
using SIGERSA.Domain.Storage;

namespace SIGERSA.Application.Users;

public sealed record PublicRegistrationRequest(
    string NombreCompleto,
    string TipoIdentificacion,
    string Identificacion,
    string Correo,
    string? Telefono,
    string Rol,
    string Password,
    bool TermsAccepted);

public sealed record PublicRegistrationResult(Guid Id, string Status);

public sealed class PublicRegistrationRequestValidator : AbstractValidator<PublicRegistrationRequest>
{
    private static readonly string[] AllowedRoles = ["ADMINISTRADOR_EMPRESA", "USUARIO_DELEGADO"];
    private static readonly string[] AllowedIdentificationTypes = ["CEDULA", "PASAPORTE"];

    public PublicRegistrationRequestValidator()
    {
        RuleFor(request => request.NombreCompleto).NotEmpty().MaximumLength(250);
        RuleFor(request => request.TipoIdentificacion)
            .Must(value => AllowedIdentificationTypes.Contains(value?.Trim().ToUpperInvariant(), StringComparer.Ordinal))
            .WithMessage("El tipo de identificación debe ser cédula o pasaporte.");
        RuleFor(request => request.Identificacion).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Correo).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.Telefono).NotEmpty().MaximumLength(40);
        RuleFor(request => request.Rol)
            .Must(value => AllowedRoles.Contains(value?.Trim().ToUpperInvariant(), StringComparer.Ordinal))
            .WithMessage("Solo puede solicitar el rol Administrador de Empresa o Usuario Delegado.");
        RuleFor(request => request.Password)
            .NotEmpty().MinimumLength(8).MaximumLength(128)
            .Matches("[A-Z]").WithMessage("La contraseña debe contener una mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe contener una minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe contener un número.")
            .Matches("[^a-zA-Z0-9]").WithMessage("La contraseña debe contener un símbolo.");
        RuleFor(request => request.TermsAccepted).Equal(true)
            .WithMessage("Debe aceptar los términos y la política de privacidad.");
    }
}

public sealed class PublicRegistrationService(
    IUsuarioRepository repository,
    IFileStorage storage,
    IPasswordService passwordService,
    IValidator<PublicRegistrationRequest> validator,
    TimeProvider timeProvider)
{
    private const long MaximumAuthorizationSize = 5 * 1024 * 1024;
    private static readonly string[] AllowedMimeTypes = ["application/pdf", "image/jpeg", "image/png"];

    public async Task<PublicRegistrationResult> RegisterAsync(
        PublicRegistrationRequest request,
        string bucketName,
        string originalName,
        string mimeType,
        long fileSize,
        Stream content,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        ValidateAuthorizationLetter(originalName, mimeType, fileSize);

        var normalizedEmail = request.Correo.Trim().ToLowerInvariant();
        var normalizedIdentification = NormalizeIdentification(request.Identificacion);
        if (await repository.PublicRegistrationExistsAsync(
                normalizedEmail, normalizedIdentification, cancellationToken))
            throw new InvalidOperationException("Ya existe una solicitud o usuario con ese correo o identificación.");

        var userId = Guid.NewGuid();
        var path = $"autorizaciones/{userId:N}/{Guid.NewGuid():N}{Extension(mimeType)}";
        var stored = await storage.UploadAsync(
            new StorageUpload(bucketName, path, mimeType, content), cancellationToken);
        var document = new SupportingDocument(
            Guid.NewGuid(), stored.BucketName, stored.SupabasePath,
            Path.GetFileName(originalName), stored.FileSize, stored.MimeType, stored.Sha256Hash);
        var draft = new PublicUserRegistrationDraft(
            userId,
            request.NombreCompleto.Trim(),
            normalizedEmail,
            request.TipoIdentificacion.Trim().ToUpperInvariant(),
            normalizedIdentification,
            request.Telefono?.Trim(),
            request.Rol.Trim().ToUpperInvariant(),
            passwordService.Hash(request.Password),
            timeProvider.GetUtcNow(),
            document);
        await repository.CreatePublicRegistrationAsync(draft, cancellationToken);
        return new PublicRegistrationResult(userId, "PENDIENTE_VALIDACION");
    }

    private static void ValidateAuthorizationLetter(string originalName, string mimeType, long fileSize)
    {
        if (string.IsNullOrWhiteSpace(originalName) || Path.GetFileName(originalName).Length > 255)
            throw new InvalidDataException("El nombre de la carta de autorización no es válido.");
        if (!AllowedMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("La carta debe estar en formato PDF, JPG o PNG.");
        if (fileSize is <= 0 or > MaximumAuthorizationSize)
            throw new InvalidDataException("La carta de autorización no puede superar 5 MB.");
    }

    private static string NormalizeIdentification(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string Extension(string mimeType) => mimeType.ToLowerInvariant() switch
    {
        "application/pdf" => ".pdf",
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        _ => throw new InvalidDataException("Tipo de documento no permitido.")
    };
}
