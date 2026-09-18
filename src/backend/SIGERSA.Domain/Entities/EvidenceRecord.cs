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
    int? SourceItem = null,
    Guid? IdempotencyKey = null,
    double? Latitude = null,
    double? Longitude = null,
    double? AccuracyMeters = null);

public sealed record EvidenceSummary(
    Guid Id,
    Guid EvaluationId,
    string EvaluationNumber,
    string EstablishmentName,
    Guid UploadedBy,
    string UploadedByName,
    string OriginalName,
    long FileSize,
    string MimeType,
    string EvidenceType,
    string SynchronizationStatus,
    DateTimeOffset UploadedAt,
    int? SourceItem,
    string? ItemCode,
    string? ItemTitle);

public sealed record EvidencesPage(IReadOnlyList<EvidenceSummary> Items, int Page, int PageSize, int Total);
public sealed record EvidenceSearch(
    string? Search, string? EvidenceType, Guid? EvaluationId, int Page, int PageSize, Guid ActorId,
    Guid? CompanyScope, bool GlobalScope, bool AssignedOnly);
public sealed record EvidenceStorageReference(
    Guid Id, string BucketName, string SupabasePath, string OriginalName, string MimeType);
