using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Storage;

namespace SIGERSA.Application.Evidences;

public sealed class EvidenceService(IFileStorage storage, IEvidenceRepository repository)
{
    public Task<EvidencesPage> SearchAsync(
        string? search, string? evidenceType, int page, int pageSize,
        EvidenceActor actor, CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        var global = HasRole(actor, "ADMINISTRADOR") || HasRole(actor, "COORDINADOR");
        var assigned = HasRole(actor, "TECNICO_EVALUADOR") && !global;
        var company = global || assigned ? (Guid?)null : actor.CompanyId
            ?? throw new ForbiddenException("El usuario no tiene un ámbito empresarial válido.");
        return repository.SearchAsync(new EvidenceSearch(
            Normalize(search)?.ToLowerInvariant(), Normalize(evidenceType)?.ToUpperInvariant(),
            Math.Max(page, 1), Math.Clamp(pageSize, 5, 100), actor.UserId,
            company, global, assigned), cancellationToken);
    }

    public async Task<EvidenceDownload> DownloadAsync(
        Guid evidenceId, EvidenceActor actor, CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        var global = HasRole(actor, "ADMINISTRADOR") || HasRole(actor, "COORDINADOR");
        var assigned = HasRole(actor, "TECNICO_EVALUADOR") && !global;
        var company = global || assigned ? (Guid?)null : actor.CompanyId
            ?? throw new ForbiddenException("El usuario no tiene un ámbito empresarial válido.");
        var reference = await repository.GetAuthorizedAsync(
            evidenceId, actor.UserId, company, global, assigned, cancellationToken)
            ?? throw new KeyNotFoundException("La evidencia no existe o no está disponible para el usuario.");
        var content = await storage.DownloadAsync(reference.BucketName, reference.SupabasePath, cancellationToken);
        return new EvidenceDownload(content, reference.MimeType, reference.OriginalName);
    }

    public async Task<StorageUploadAuthorization> AuthorizeUploadAsync(
        Guid evaluationId,
        Guid uploadedBy,
        Guid idempotencyKey,
        string bucketName,
        string originalName,
        string mimeType,
        long fileSize,
        CancellationToken cancellationToken)
    {
        ValidateIdentifiers(evaluationId, uploadedBy, idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalName);
        if (!await repository.CanUploadAsync(uploadedBy, evaluationId, cancellationToken))
        {
            throw new UnauthorizedAccessException("El usuario no está asignado a esta evaluación.");
        }

        var path = BuildPath(evaluationId, idempotencyKey, mimeType);
        return await storage.CreateUploadAuthorizationAsync(
            bucketName, path, mimeType, fileSize, cancellationToken);
    }

    public async Task<EvidenceRecord> ConfirmUploadAsync(
        Guid evaluationId,
        Guid uploadedBy,
        Guid idempotencyKey,
        string bucketName,
        string supabasePath,
        string originalName,
        string mimeType,
        string evidenceType,
        long declaredFileSize,
        string declaredSha256Hash,
        CancellationToken cancellationToken)
    {
        ValidateIdentifiers(evaluationId, uploadedBy, idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceType);
        if (!await repository.CanUploadAsync(uploadedBy, evaluationId, cancellationToken))
        {
            throw new UnauthorizedAccessException("El usuario no está asignado a esta evaluación.");
        }

        var expectedPath = BuildPath(evaluationId, idempotencyKey, mimeType);
        if (!string.Equals(supabasePath, expectedPath, StringComparison.Ordinal))
        {
            throw new InvalidDataException("La ruta de evidencia no corresponde a la autorización emitida.");
        }

        var stored = await storage.VerifyAsync(bucketName, supabasePath, mimeType, cancellationToken);
        if (stored.FileSize != declaredFileSize ||
            !string.Equals(stored.Sha256Hash, declaredSha256Hash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("El tamaño o hash de la evidencia no coincide con el archivo cargado.");
        }

        var safeOriginalName = Path.GetFileName(originalName);
        if (safeOriginalName.Length is 0 or > 255)
        {
            throw new InvalidDataException("El nombre original de la evidencia no es válido.");
        }

        var evidence = new EvidenceRecord(
            Guid.NewGuid(), evaluationId, uploadedBy, stored.BucketName, stored.SupabasePath,
            safeOriginalName, Path.GetFileName(stored.SupabasePath), stored.FileSize,
            stored.MimeType, stored.Sha256Hash, evidenceType, idempotencyKey);
        await repository.CreateAsync(evidence, cancellationToken);
        return evidence;
    }

    public async Task<EvidenceRecord> UploadAsync(
        Guid evaluationId,
        Guid uploadedBy,
        string bucketName,
        string originalName,
        string mimeType,
        string evidenceType,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceType);
        if (!await repository.CanUploadAsync(uploadedBy, evaluationId, cancellationToken))
        {
            throw new UnauthorizedAccessException("El usuario no está asignado a esta evaluación.");
        }

        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        var safeName = $"{Guid.NewGuid():N}{extension}";
        var path = $"evaluaciones/{evaluationId:N}/{safeName}";
        var stored = await storage.UploadAsync(
            new StorageUpload(bucketName, path, mimeType, content),
            cancellationToken);
        var evidence = new EvidenceRecord(
            Guid.NewGuid(),
            evaluationId,
            uploadedBy,
            stored.BucketName,
            stored.SupabasePath,
            Path.GetFileName(originalName),
            safeName,
            stored.FileSize,
            stored.MimeType,
            stored.Sha256Hash,
            evidenceType);
        await repository.CreateAsync(evidence, cancellationToken);
        return evidence;
    }

    private static string BuildPath(Guid evaluationId, Guid idempotencyKey, string mimeType)
    {
        var extension = mimeType.Trim().ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "video/mp4" => ".mp4",
            _ => throw new InvalidDataException("El tipo MIME no está permitido.")
        };
        return $"evaluaciones/{evaluationId:N}/{idempotencyKey:N}{extension}";
    }

    private static void ValidateIdentifiers(Guid evaluationId, Guid uploadedBy, Guid idempotencyKey)
    {
        if (evaluationId == Guid.Empty || uploadedBy == Guid.Empty || idempotencyKey == Guid.Empty)
        {
            throw new ArgumentException("Los identificadores de evidencia son obligatorios.");
        }
    }

    private static void EnsureReader(EvidenceActor actor)
    {
        if (!actor.Roles.Any(role => role is "ADMINISTRADOR" or "ADMINISTRADOR_EMPRESA" or "USUARIO_DELEGADO" or "COORDINADOR" or "TECNICO_EVALUADOR"))
            throw new ForbiddenException("No tiene permisos para consultar evidencias.");
    }
    private static bool HasRole(EvidenceActor actor, string role) => actor.Roles.Contains(role, StringComparer.Ordinal);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record EvidenceActor(Guid UserId, string[] Roles, Guid? CompanyId);
public sealed record EvidenceDownload(Stream Content, string MimeType, string FileName);
