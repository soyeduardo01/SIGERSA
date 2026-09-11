namespace SIGERSA.Application.Authentication;

public sealed class AuthFlowOptions
{
    public const string SectionName = "Authentication:Flow";

    public int MaximumLoginAttempts { get; init; } = 5;

    public int LockoutMinutes { get; init; } = 15;

    public int OtpLifetimeMinutes { get; init; } = 10;

    public int MaximumOtpAttempts { get; init; } = 5;

    public int RefreshTokenDays { get; init; } = 14;

    public bool TwoFactorEnabled { get; init; }
}
