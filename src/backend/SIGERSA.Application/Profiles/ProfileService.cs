using FluentValidation;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Security;

namespace SIGERSA.Application.Profiles;

public sealed class ProfileService(
    IUserProfileRepository repository,
    IPasswordService passwordService,
    ISupabaseMfaGateway supabaseMfa,
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
        var profile = await GetAccountAsync(userId, cancellationToken);
        if (profile.RowVersion != request.RowVersion) throw new OptimisticConcurrencyException(userId);
        var draft = new UserProfileDraft(
            request.FullName.Trim(),
            request.Email.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            request.RowVersion);
        if (profile.SupabaseAuthUserId is { } supabaseUserId &&
            !string.Equals(profile.Email, draft.Email, StringComparison.OrdinalIgnoreCase))
        {
            await supabaseMfa.UpdateUserAsync(supabaseUserId, draft.Email, null, cancellationToken);
        }
        if (!await repository.UpdateAsync(userId, draft, cancellationToken))
        {
            if (profile.SupabaseAuthUserId is { } rollbackId &&
                !string.Equals(profile.Email, draft.Email, StringComparison.OrdinalIgnoreCase))
                await supabaseMfa.UpdateUserAsync(rollbackId, profile.Email, null, cancellationToken);
            throw new OptimisticConcurrencyException(userId);
        }
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
        if (profile.RowVersion != request.RowVersion) throw new OptimisticConcurrencyException(userId);
        if (!passwordService.Verify(profile.PasswordHash, request.CurrentPassword))
            throw new ArgumentException("La contraseña actual no es correcta.");
        if (passwordService.Verify(profile.PasswordHash, request.NewPassword))
            throw new ArgumentException("La nueva contraseña debe ser diferente de la actual.");
        if (profile.SupabaseAuthUserId is { } supabaseUserId)
            await supabaseMfa.UpdateUserAsync(supabaseUserId, null, request.NewPassword, cancellationToken);
        if (!await repository.ChangePasswordAsync(
                userId,
                passwordService.Hash(request.NewPassword),
                request.RowVersion,
                cancellationToken))
        {
            if (profile.SupabaseAuthUserId is { } rollbackId)
                await supabaseMfa.UpdateUserAsync(rollbackId, null, request.CurrentPassword, cancellationToken);
            throw new OptimisticConcurrencyException(userId);
        }
    }

    public async Task<MfaEnrollmentBootstrap> BeginMfaEnrollmentAsync(
        Guid userId,
        BeginMfaEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await GetAccountAsync(userId, cancellationToken);
        if (profile.MfaEnabled) throw new InvalidOperationException("MFA ya está habilitado.");
        EnsureCurrentPassword(profile, request.CurrentPassword);
        var supabaseUserId = await supabaseMfa.EnsureUserAsync(
            profile.SupabaseAuthUserId,
            profile.Email,
            request.CurrentPassword,
            profile.FullName,
            cancellationToken);
        if (profile.SupabaseAuthUserId != supabaseUserId)
            await repository.SetSupabaseIdentityAsync(userId, supabaseUserId, cancellationToken);
        return new MfaEnrollmentBootstrap(profile.Email);
    }

    public async Task<ProfileResponse> CompleteMfaEnrollmentAsync(
        Guid userId,
        CompleteMfaEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await GetAccountAsync(userId, cancellationToken);
        if (profile.SupabaseAuthUserId is null)
            throw new InvalidOperationException("Primero inicie la configuración de MFA.");
        var verifiedUserId = await supabaseMfa.ValidateAal2TokenAsync(
            request.SupabaseAccessToken, cancellationToken);
        if (verifiedUserId != profile.SupabaseAuthUserId)
            throw new UnauthorizedAccessException("La verificación MFA no corresponde al usuario autenticado.");
        await repository.SetMfaAsync(userId, true, request.FactorId, cancellationToken);
        return await GetAsync(userId, cancellationToken);
    }

    public async Task<ProfileResponse> DisableMfaAsync(
        Guid userId,
        DisableMfaRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await GetAccountAsync(userId, cancellationToken);
        EnsureCurrentPassword(profile, request.CurrentPassword);
        if (!profile.MfaEnabled || profile.MfaFactorId != request.FactorId ||
            profile.SupabaseAuthUserId is null)
            throw new InvalidOperationException("El factor MFA activo no coincide con la solicitud.");
        var verifiedUserId = await supabaseMfa.ValidateAal2TokenAsync(
            request.SupabaseAccessToken, cancellationToken);
        if (verifiedUserId != profile.SupabaseAuthUserId)
            throw new UnauthorizedAccessException("La verificación MFA no corresponde al usuario autenticado.");
        await repository.SetMfaAsync(userId, false, null, cancellationToken);
        return await GetAsync(userId, cancellationToken);
    }

    private async Task<UserProfileAccount> GetAccountAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await repository.GetAsync(userId, cancellationToken)
        ?? throw new KeyNotFoundException("El perfil del usuario no existe o está inactivo.");

    private void EnsureCurrentPassword(UserProfileAccount profile, string password)
    {
        if (string.IsNullOrWhiteSpace(password) || !passwordService.Verify(profile.PasswordHash, password))
            throw new ArgumentException("La contraseña actual no es correcta.");
    }

    private static ProfileResponse ToResponse(UserProfileAccount profile) =>
        new(profile.Id, profile.FullName, profile.Email, profile.Phone, profile.Status,
            profile.LastAccessAt, profile.MfaEnabled, profile.RowVersion);
}
