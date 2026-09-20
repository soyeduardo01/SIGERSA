namespace SIGERSA.Infrastructure.Configuration;

public sealed class WebPushOptions
{
    public const string SectionName = "WebPush";

    public bool Enabled { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string PublicKey { get; init; } = string.Empty;
    public string PrivateKey { get; init; } = string.Empty;
    public int TimeToLiveSeconds { get; init; } = 3600;
    public string[] AllowedEndpointHosts { get; init; } =
    [
        "fcm.googleapis.com",
        ".push.services.mozilla.com",
        "web.push.apple.com",
        ".notify.windows.com"
    ];
}
