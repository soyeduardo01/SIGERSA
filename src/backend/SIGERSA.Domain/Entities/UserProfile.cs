namespace SIGERSA.Domain.Entities;

public sealed record UserProfileAccount(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    string PasswordHash,
    string Status,
    DateTimeOffset? LastAccessAt,
    Guid? SupabaseAuthUserId,
    bool MfaEnabled,
    Guid? MfaFactorId,
    long RowVersion);

public sealed record UserProfileDraft(
    string FullName,
    string Email,
    string? Phone,
    long RowVersion);
