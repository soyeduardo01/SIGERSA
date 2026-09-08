namespace SIGERSA.Infrastructure.Configuration;

public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    public required string Url { get; init; }

    public required string Key { get; init; }

    public string DefaultBucketName { get; init; } = "evidencias";

    public long MaxFileSizeBytes { get; init; } = 25 * 1024 * 1024;

    public string[] AllowedMimeTypes { get; init; } =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
        "video/mp4"
    ];
}
