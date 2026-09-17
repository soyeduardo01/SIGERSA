using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Storage;

namespace SIGERSA.Application.Documents;

public sealed class SupportingDocumentService(
    ISupportingDocumentRepository repository,
    IFileStorage storage)
{
    private const long MaximumSize = 5 * 1024 * 1024;
    private static readonly string[] AllowedMimeTypes = ["application/pdf", "image/jpeg", "image/png"];

    public async Task<SupportingDocumentDownload> DownloadUserAuthorizationAsync(
        Guid targetUserId, DocumentActor actor, CancellationToken cancellationToken)
    {
        if (!actor.Roles.Any(role => role is "ADMINISTRADOR" or "ADMINISTRADOR_EMPRESA" or "COORDINADOR"))
            throw new ForbiddenException("No tiene permisos para revisar cartas de autorización.");
        var global = actor.Roles.Any(role => role is "ADMINISTRADOR" or "COORDINADOR");
        var companyScope = global ? (Guid?)null : actor.CompanyId
            ?? throw new ForbiddenException("El usuario no tiene un ámbito empresarial válido.");
        var reference = await repository.GetUserAuthorizationAsync(
            targetUserId, companyScope, global, cancellationToken)
            ?? throw new KeyNotFoundException("La carta de autorización no existe o no está disponible.");
        var content = await storage.DownloadAsync(
            reference.BucketName, reference.SupabasePath, cancellationToken);
        return new SupportingDocumentDownload(content, reference.MimeType, reference.OriginalName);
    }

    public async Task<Guid> UploadUserAuthorizationAsync(
        Guid targetUserId, string bucketName, string originalName, string mimeType,
        long fileSize, Stream content, DocumentActor actor, CancellationToken cancellationToken)
    {
        if (!actor.Roles.Any(role => role is "ADMINISTRADOR" or "ADMINISTRADOR_EMPRESA"))
            throw new ForbiddenException("No tiene permisos para registrar cartas de autorización.");
        ValidateFile(originalName, mimeType, fileSize);
        var global = actor.Roles.Contains("ADMINISTRADOR", StringComparer.Ordinal);
        Guid? companyScope = global ? null : actor.CompanyId
            ?? throw new ForbiddenException("El usuario no tiene un ámbito empresarial válido.");
        if (!await repository.CanAttachToUserAsync(targetUserId, companyScope, global, cancellationToken))
            throw new KeyNotFoundException("El usuario no existe o no pertenece al ámbito autorizado.");
        var path = $"autorizaciones/{targetUserId:N}/{Guid.NewGuid():N}{Extension(mimeType)}";
        var stored = await storage.UploadAsync(new StorageUpload(bucketName, path, mimeType, content), cancellationToken);
        var document = ToDocument(stored, originalName);
        return await repository.SaveUserAuthorizationAsync(targetUserId, document, actor.UserId, cancellationToken);
    }

    public async Task<Guid> UploadRequestDocumentAsync(
        Guid requestId, string documentType, bool required, string bucketName,
        string originalName, string mimeType, long fileSize, Stream content,
        DocumentActor actor, CancellationToken cancellationToken)
    {
        if (!actor.Roles.Any(role => role is "ADMINISTRADOR" or "ADMINISTRADOR_EMPRESA" or "USUARIO_DELEGADO" or "COORDINADOR"))
            throw new ForbiddenException("No tiene permisos para adjuntar documentos a la solicitud.");
        ValidateFile(originalName, mimeType, fileSize);
        if (string.IsNullOrWhiteSpace(documentType) || documentType.Length > 80)
            throw new ArgumentException("El tipo de documento no es válido.");
        var global = actor.Roles.Any(role => role is "ADMINISTRADOR" or "COORDINADOR");
        Guid? companyScope = global ? null : actor.CompanyId
            ?? throw new ForbiddenException("El usuario no tiene un ámbito empresarial válido.");
        if (!await repository.CanAttachToRequestAsync(requestId, actor.UserId, companyScope, global, cancellationToken))
            throw new KeyNotFoundException("La solicitud no existe, ya fue enviada o no pertenece al ámbito autorizado.");
        var path = $"solicitudes/{requestId:N}/{Guid.NewGuid():N}{Extension(mimeType)}";
        var stored = await storage.UploadAsync(new StorageUpload(bucketName, path, mimeType, content), cancellationToken);
        var document = ToDocument(stored, originalName);
        return await repository.SaveRequestDocumentAsync(
            requestId, documentType.Trim().ToUpperInvariant(), required, document, actor.UserId, cancellationToken);
    }

    public Task<IReadOnlyList<RequestSupportingDocument>> GetRequestDocumentsAsync(
        Guid requestId, DocumentActor actor, CancellationToken cancellationToken)
    {
        EnsureRequestDocumentReader(actor);
        var global = actor.Roles.Any(role => role is "ADMINISTRADOR" or "COORDINADOR");
        return repository.GetRequestDocumentsAsync(
            requestId, actor.UserId, global ? null : actor.CompanyId, global, cancellationToken);
    }

    public async Task<SupportingDocumentDownload> DownloadRequestDocumentAsync(
        Guid requestId, Guid documentId, DocumentActor actor, CancellationToken cancellationToken)
    {
        EnsureRequestDocumentReader(actor);
        var global = actor.Roles.Any(role => role is "ADMINISTRADOR" or "COORDINADOR");
        var reference = await repository.GetRequestDocumentAsync(
            requestId, documentId, actor.UserId, global ? null : actor.CompanyId, global, cancellationToken)
            ?? throw new KeyNotFoundException("El documento no existe o no está disponible para el usuario.");
        var content = await storage.DownloadAsync(reference.BucketName, reference.SupabasePath, cancellationToken);
        return new SupportingDocumentDownload(content, reference.MimeType, reference.OriginalName);
    }

    private static void EnsureRequestDocumentReader(DocumentActor actor)
    {
        if (!actor.Roles.Any(role => role is "ADMINISTRADOR" or "ADMINISTRADOR_EMPRESA" or
                "USUARIO_DELEGADO" or "COORDINADOR" or "TECNICO_EVALUADOR"))
            throw new ForbiddenException("No tiene permisos para consultar documentos de la solicitud.");
    }

    private static SupportingDocument ToDocument(StoredFile stored, string originalName) => new(
        Guid.NewGuid(), stored.BucketName, stored.SupabasePath, Path.GetFileName(originalName),
        stored.FileSize, stored.MimeType, stored.Sha256Hash);

    private static void ValidateFile(string originalName, string mimeType, long fileSize)
    {
        if (string.IsNullOrWhiteSpace(originalName) || Path.GetFileName(originalName).Length > 255)
            throw new InvalidDataException("El nombre del documento no es válido.");
        if (!AllowedMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Solo se permiten documentos PDF, JPG o PNG.");
        if (fileSize is <= 0 or > MaximumSize)
            throw new InvalidDataException("El documento debe pesar entre 1 byte y 5 MB.");
    }

    private static string Extension(string mimeType) => mimeType.ToLowerInvariant() switch
    {
        "application/pdf" => ".pdf",
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        _ => throw new InvalidDataException("Tipo de documento no permitido.")
    };
}

public sealed record DocumentActor(Guid UserId, string[] Roles, Guid? CompanyId);
public sealed record SupportingDocumentDownload(Stream Content, string MimeType, string FileName);
