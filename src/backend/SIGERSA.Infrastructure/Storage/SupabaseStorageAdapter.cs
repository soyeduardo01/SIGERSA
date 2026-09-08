using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SIGERSA.Domain.Storage;
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

        if (!_options.AllowedMimeTypes.Contains(upload.MimeType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("El tipo MIME no está permitido.");
        }

        var bytes = await ReadBoundedAsync(upload.Content, _options.MaxFileSizeBytes, cancellationToken);
        if (!FileSignatureValidator.HasValidSignature(upload.MimeType, bytes))
        {
            throw new InvalidDataException("La firma del archivo no coincide con el tipo MIME declarado.");
        }

        var fileOptions = new Supabase.Storage.FileOptions
        {
            ContentType = upload.MimeType,
            Upsert = false
        };

        await client.Storage
            .From(upload.BucketName)
            .Upload(bytes, upload.SupabasePath, fileOptions, cancellationToken: cancellationToken);

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new StoredFile(
            upload.BucketName,
            upload.SupabasePath,
            bytes.LongLength,
            upload.MimeType,
            hash);
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
