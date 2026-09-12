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

    public static bool IsServerCredential(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var key = value.Trim();
        if (key.StartsWith("sb_secret_", StringComparison.Ordinal))
            return key.Length >= 32;

        var segments = key.Split('.');
        if (segments.Length != 3 || segments.Any(string.IsNullOrWhiteSpace)) return false;

        try
        {
            var payload = segments[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
            using var document = System.Text.Json.JsonDocument.Parse(Convert.FromBase64String(payload));
            return document.RootElement.TryGetProperty("role", out var role) &&
                   role.GetString() == "service_role";
        }
        catch (FormatException)
        {
            return false;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
