namespace SIGERSA.Application.Operations;

public sealed class ReportOptions
{
    public const string SectionName = "Reports";
    public string BucketName { get; init; } = "evidencias";
    public string? BrowserExecutablePath { get; init; }
    public string VerificationBaseUrl { get; init; } = "http://localhost:5000/api/v1/reports/verify";
}
