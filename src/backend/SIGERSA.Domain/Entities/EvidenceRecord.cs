namespace SIGERSA.Domain.Entities;

public sealed record EvidenceRecord(
    Guid Id,
    Guid EvaluationId,
    Guid UploadedBy,
    string BucketName,
    string SupabasePath,
    string OriginalName,
    string SafeName,
    long FileSize,
    string MimeType,
    string Sha256Hash,
    string EvidenceType,
    Guid? IdempotencyKey = null);
