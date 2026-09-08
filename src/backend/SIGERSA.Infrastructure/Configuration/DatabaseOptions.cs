namespace SIGERSA.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public required string ConnectionString { get; init; }

    public int DefaultCommandTimeoutSeconds { get; init; } = 30;
}
