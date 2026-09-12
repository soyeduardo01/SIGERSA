using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Storage;
using Supabase.Storage.Exceptions;
using SIGERSA.Infrastructure.Configuration;

namespace SIGERSA.Infrastructure.Storage;

public sealed class SupabaseStorageAdapter(
    Supabase.Client client,
    IOptions<SupabaseOptions> options) : IFileStorage
{
    private readonly SupabaseOptions _options = options.Value;

    public async Task<StoredFile> UploadAsync(
        StorageUpload upload,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(upload.BucketName, upload.SupabasePath);

        var bytes = await ReadBoundedAsync(upload.Content, _options.MaxFileSizeBytes, cancellationToken);
        ValidateBytes(upload.MimeType, bytes);

        var fileOptions = new Supabase.Storage.FileOptions
        {
            ContentType = upload.MimeType,
            Upsert = false
        };

        try
        {
            await client.Storage
                .From(upload.BucketName)
                .Upload(bytes, upload.SupabasePath, fileOptions, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is SupabaseStorageException or HttpRequestException)
        {
            throw new FileStorageUnavailableException(
                "No fue posible almacenar la carta de autorización. Verifique la configuración del almacenamiento e inténtelo nuevamente.",
                exception);
        }

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new StoredFile(
            upload.BucketName,
            upload.SupabasePath,
            bytes.LongLength,
            upload.MimeType,
            hash);
    }

    public async Task<StorageUploadAuthorization> CreateUploadAuthorizationAsync(
        string bucketName,
        string supabasePath,
        string mimeType,
        long fileSize,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateLocation(bucketName, supabasePath);
        if (!_options.AllowedMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("El tipo MIME no está permitido.");
        }
        if (fileSize <= 0 || fileSize > _options.MaxFileSizeBytes)
        {
            throw new InvalidDataException("El tamaño del archivo no está permitido.");
        }
        var authorization = await client.Storage.From(bucketName).CreateUploadSignedUrl(supabasePath);
        return new StorageUploadAuthorization(
            bucketName,
            supabasePath,
            authorization.Token,
            authorization.SignedUrl.ToString());
    }

    public async Task<StoredFile> VerifyAsync(
        string bucketName,
        string supabasePath,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, supabasePath);
        var bytes = await client.Storage
            .From(bucketName)
            .Download(supabasePath, (EventHandler<float>?)null, cancellationToken, null);
        if (bytes.LongLength > _options.MaxFileSizeBytes)
        {
            throw new InvalidDataException("El archivo excede el tamaño máximo permitido.");
        }

        ValidateBytes(mimeType, bytes);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new StoredFile(bucketName, supabasePath, bytes.LongLength, mimeType, hash);
    }

    public async Task<Stream> DownloadAsync(
        string bucketName,
        string supabasePath,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, supabasePath);

        var bytes = await client.Storage
            .From(bucketName)
            .Download(supabasePath, (EventHandler<float>?)null, cancellationToken, null);

        return new MemoryStream(bytes, writable: false);
    }

    private static void ValidateLocation(string bucketName, string supabasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(supabasePath);

        if (bucketName.Contains('/') || bucketName.Contains('\\'))
        {
            throw new ArgumentException("El nombre del bucket no puede contener separadores.", nameof(bucketName));
        }

        if (supabasePath.StartsWith('/') ||
            supabasePath.Contains('\\') ||
            supabasePath.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("La ruta de Supabase no es válida.", nameof(supabasePath));
        }
    }

    private void ValidateBytes(string mimeType, ReadOnlySpan<byte> bytes)
    {
        if (!_options.AllowedMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("El tipo MIME no está permitido.");
        }

        if (!FileSignatureValidator.HasValidSignature(mimeType, bytes))
        {
            throw new InvalidDataException("La firma del archivo no coincide con el tipo MIME declarado.");
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream source,
        long maxFileSizeBytes,
        CancellationToken cancellationToken)
    {
        if (maxFileSizeBytes <= 0 || maxFileSizeBytes > int.MaxValue - 1)
        {
            throw new InvalidOperationException("El límite de archivo configurado no es válido.");
        }

        if (source.CanSeek && source.Length > maxFileSizeBytes)
        {
            throw new InvalidDataException("El archivo excede el tamaño máximo permitido.");
        }

        using var target = new MemoryStream();
        var buffer = new byte[81920];
        long totalRead = 0;
        int read;

        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            totalRead += read;
            if (totalRead > maxFileSizeBytes)
            {
                throw new InvalidDataException("El archivo excede el tamaño máximo permitido.");
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return target.ToArray();
    }
}
