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
}
