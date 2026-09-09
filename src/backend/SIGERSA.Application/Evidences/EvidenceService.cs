using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Storage;

namespace SIGERSA.Application.Evidences;

public sealed class EvidenceService(IFileStorage storage, IEvidenceRepository repository)
{
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
}
