namespace SIGERSA.Domain.Entities;

public sealed record SupportingDocument(
    Guid Id,
    string BucketName,
    string SupabasePath,
    string OriginalName,
    long FileSize,
    string MimeType,
    string Sha256Hash);

public sealed record SupportingDocumentReference(
    string BucketName,
    string SupabasePath,
    string OriginalName,
    string MimeType);
