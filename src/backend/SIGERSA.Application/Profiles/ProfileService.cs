using FluentValidation;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Security;

namespace SIGERSA.Application.Profiles;

public sealed class ProfileService(
    IUserProfileRepository repository,
    IPasswordService passwordService,
    IValidator<UpdateProfileRequest> updateValidator,
    IValidator<ChangePasswordRequest> passwordValidator)
{
    public async Task<ProfileResponse> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await repository.GetAsync(userId, cancellationToken)
            ?? throw new KeyNotFoundException("El perfil del usuario no existe o está inactivo.");
        return ToResponse(profile);
    }

    public async Task<ProfileResponse> UpdateAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var draft = new UserProfileDraft(
            request.FullName.Trim(),
            request.Email.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            request.RowVersion);
        if (!await repository.UpdateAsync(userId, draft, cancellationToken))
            throw new OptimisticConcurrencyException(userId);
        return await GetAsync(userId, cancellationToken);
    }

    public async Task ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        await passwordValidator.ValidateAndThrowAsync(request, cancellationToken);
        var profile = await repository.GetAsync(userId, cancellationToken)
            ?? throw new KeyNotFoundException("El perfil del usuario no existe o está inactivo.");
        if (!passwordService.Verify(profile.PasswordHash, request.CurrentPassword))
            throw new ArgumentException("La contraseña actual no es correcta.");
        if (passwordService.Verify(profile.PasswordHash, request.NewPassword))
            throw new ArgumentException("La nueva contraseña debe ser diferente de la actual.");
        if (!await repository.ChangePasswordAsync(
                userId,
                passwordService.Hash(request.NewPassword),
                request.RowVersion,
                cancellationToken))
            throw new OptimisticConcurrencyException(userId);
    }

    private static ProfileResponse ToResponse(UserProfileAccount profile) =>
        new(profile.Id, profile.FullName, profile.Email, profile.Phone, profile.RowVersion);
}
