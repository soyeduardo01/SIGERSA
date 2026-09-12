using SIGERSA.Domain.Entities;

namespace SIGERSA.Domain.Repositories;

public interface IUserProfileRepository
{
    Task<UserProfileAccount?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        Guid userId,
        UserProfileDraft draft,
        CancellationToken cancellationToken = default);

    Task<bool> ChangePasswordAsync(
        Guid userId,
        string passwordHash,
        long rowVersion,
        CancellationToken cancellationToken = default);

    Task SetSupabaseIdentityAsync(
        Guid userId,
        Guid supabaseUserId,
        CancellationToken cancellationToken = default);

    Task SetMfaAsync(
        Guid userId,
        bool enabled,
        Guid? factorId,
        CancellationToken cancellationToken = default);
}
