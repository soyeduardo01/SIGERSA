namespace SIGERSA.Domain.Storage;

public interface IFileStorage
{
    Task<StoredFile> UploadAsync(StorageUpload upload, CancellationToken cancellationToken = default);

    Task<StorageUploadAuthorization> CreateUploadAuthorizationAsync(
        string bucketName,
        string supabasePath,
        string mimeType,
        long fileSize,
        CancellationToken cancellationToken = default);

    Task<StoredFile> VerifyAsync(
        string bucketName,
        string supabasePath,
        string mimeType,
        CancellationToken cancellationToken = default);

    Task<Stream> DownloadAsync(
        string bucketName,
        string supabasePath,
        CancellationToken cancellationToken = default);
}

public sealed record StorageUpload(
    string BucketName,
    string SupabasePath,
    string MimeType,
    Stream Content);

public sealed record StoredFile(
    string BucketName,
    string SupabasePath,
    long FileSize,
    string MimeType,
    string Sha256Hash);

public sealed record StorageUploadAuthorization(
    string BucketName,
    string SupabasePath,
    string Token,
    string SignedUrl);
