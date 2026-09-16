namespace SIGERSA.Application.Operations;

public sealed class ReportOptions
{
    public const string SectionName = "Reports";
    public string BucketName { get; init; } = "evidencias";
    public string? BrowserExecutablePath { get; init; }
}
