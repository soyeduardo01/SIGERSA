using SIGERSA.Application.Profiles;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;
using SIGERSA.Infrastructure.Security;

namespace SIGERSA.Tests.Application;

public sealed class ProfileServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task UpdateNormalizesPersonalInformation()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        await service.UpdateAsync(UserId,
            new UpdateProfileRequest("  Ana Pérez  ", " ANA@EXAMPLE.COM ", " 809-555-0101 ", 1),
            CancellationToken.None);

        Assert.Equal("Ana Pérez", repository.Profile.FullName);
        Assert.Equal("ana@example.com", repository.Profile.Email);
        Assert.Equal("809-555-0101", repository.Profile.Phone);
    }

    [Fact]
    public async Task ChangePasswordRejectsAnIncorrectCurrentPassword()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => service.ChangePasswordAsync(
            UserId,
            new ChangePasswordRequest("Incorrecta-1!", "Nueva-Segura-2!", "Nueva-Segura-2!", 1),
            CancellationToken.None));

        Assert.False(repository.PasswordChanged);
    }

    [Fact]
    public async Task ChangePasswordRaisesConcurrencyErrorWhenVersionChanged()
    {
        var repository = new FakeRepository { AllowPasswordChange = false };
        var service = CreateService(repository);

        await Assert.ThrowsAsync<OptimisticConcurrencyException>(() => service.ChangePasswordAsync(
            UserId,
            new ChangePasswordRequest("Actual-Segura-1!", "Nueva-Segura-2!", "Nueva-Segura-2!", 1),
            CancellationToken.None));
    }

    private static ProfileService CreateService(FakeRepository repository) => new(
        repository,
        new BcryptPasswordService(),
        new UpdateProfileRequestValidator(),
        new ChangePasswordRequestValidator());

    private sealed class FakeRepository : IUserProfileRepository
    {
        private readonly BcryptPasswordService passwordService = new();
        public bool AllowPasswordChange { get; init; } = true;
        public bool PasswordChanged { get; private set; }
        public UserProfileAccount Profile { get; private set; }

        public FakeRepository()
        {
            Profile = new UserProfileAccount(UserId, "Ana", "ana@example.com", null,
                passwordService.Hash("Actual-Segura-1!"), 1);
        }

        public Task<UserProfileAccount?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserProfileAccount?>(Profile);

        public Task<bool> UpdateAsync(Guid userId, UserProfileDraft draft, CancellationToken cancellationToken = default)
        {
            Profile = Profile with { FullName = draft.FullName, Email = draft.Email, Phone = draft.Phone, RowVersion = draft.RowVersion + 1 };
            return Task.FromResult(true);
        }

        public Task<bool> ChangePasswordAsync(Guid userId, string passwordHash, long rowVersion, CancellationToken cancellationToken = default)
        {
            PasswordChanged = AllowPasswordChange;
            return Task.FromResult(AllowPasswordChange);
        }
    }
}
