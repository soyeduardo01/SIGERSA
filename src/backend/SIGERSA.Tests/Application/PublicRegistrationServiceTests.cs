using FluentValidation;
using SIGERSA.Application.Users;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Security;
using SIGERSA.Domain.Storage;
using SIGERSA.Infrastructure.Security;

namespace SIGERSA.Tests.Application;

public sealed class PublicRegistrationServiceTests
{
    [Fact]
    public async Task RegistrationStoresPendingUserAndAuthorizationInSupabase()
    {
        var repository = new FakeRepository();
        var storage = new FakeStorage();
        var service = new PublicRegistrationService(
            repository, storage, new BcryptPasswordService(),
            new PublicRegistrationRequestValidator(), new FixedTimeProvider());

        var result = await service.RegisterAsync(
            ValidRequest(), "evidencias", "carta.pdf", "application/pdf", 4,
            new MemoryStream([0x25, 0x50, 0x44, 0x46]), CancellationToken.None);

        Assert.Equal("PENDIENTE_VALIDACION", result.Status);
        Assert.Equal(result.Id, repository.Created?.Id);
        Assert.Equal("ADMINISTRADOR_EMPRESA", repository.Created?.RequestedRole);
        Assert.StartsWith("$2", repository.Created?.PasswordHash);
        Assert.StartsWith($"autorizaciones/{result.Id:N}/", storage.Upload?.SupabasePath);
        Assert.Equal("evidencias", repository.Created?.AuthorizationLetter.BucketName);
    }

    [Fact]
    public async Task RegistrationRejectsInternalRolesAndMissingConsent()
    {
        var service = new PublicRegistrationService(
            new FakeRepository(), new FakeStorage(), new BcryptPasswordService(),
            new PublicRegistrationRequestValidator(), new FixedTimeProvider());

        await Assert.ThrowsAsync<ValidationException>(() => service.RegisterAsync(
            ValidRequest() with { Rol = "ADMINISTRADOR", TermsAccepted = false },
            "evidencias", "carta.pdf", "application/pdf", 4,
            new MemoryStream([0x25, 0x50, 0x44, 0x46]), CancellationToken.None));
    }

    [Fact]
    public async Task RegistrationRejectsFilesLargerThanFiveMegabytes()
    {
        var service = new PublicRegistrationService(
            new FakeRepository(), new FakeStorage(), new BcryptPasswordService(),
            new PublicRegistrationRequestValidator(), new FixedTimeProvider());

        await Assert.ThrowsAsync<InvalidDataException>(() => service.RegisterAsync(
            ValidRequest(), "evidencias", "carta.pdf", "application/pdf", 5 * 1024 * 1024 + 1,
            Stream.Null, CancellationToken.None));
    }

    private static PublicRegistrationRequest ValidRequest() => new(
        "María Pérez", "CEDULA", "001-0000000-1", "maria@example.com",
        "809-555-1212", "ADMINISTRADOR_EMPRESA", "Segura8!", true);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 9, 13, 20, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeStorage : IFileStorage
    {
        public StorageUpload? Upload { get; private set; }
        public Task<StoredFile> UploadAsync(StorageUpload upload, CancellationToken cancellationToken = default)
        {
            Upload = upload;
            return Task.FromResult(new StoredFile(
                upload.BucketName, upload.SupabasePath, 4, upload.MimeType, "hash-carta"));
        }
        public Task<StorageUploadAuthorization> CreateUploadAuthorizationAsync(string bucketName,
            string supabasePath, string mimeType, long fileSize, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<StoredFile> VerifyAsync(string bucketName, string supabasePath, string mimeType,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> DownloadAsync(string bucketName, string supabasePath,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeRepository : IUsuarioRepository
    {
        public PublicUserRegistrationDraft? Created { get; private set; }
        public Task<bool> PublicRegistrationExistsAsync(string normalizedEmail, string normalizedIdentification,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<Guid> CreatePublicRegistrationAsync(PublicUserRegistrationDraft draft,
            CancellationToken cancellationToken = default) { Created = draft; return Task.FromResult(draft.Id); }
        public Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Usuario?>(null);
        public Task ActualizarNombreAsync(Guid id, string nombreCompleto, Guid modificadoPor, long versionFila,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ManagedUsersPage> SearchManagedAsync(ManagedUsersQuery query,
            CancellationToken cancellationToken = default) => Task.FromResult(new ManagedUsersPage([], 1, 10, 0));
        public Task<IReadOnlyList<RoleOption>> GetActiveRoleOptionsAsync(
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RoleOption>>([]);
        public Task<IReadOnlyList<CompanyOption>> GetActiveCompanyOptionsAsync(
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CompanyOption>>([]);
        public Task<bool> CanActivateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<Guid> CreateManagedAsync(ManagedUserDraft draft, Guid actorId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateManagedAsync(Guid id, ManagedUserDraft draft, Guid actorId, Guid? companyScope,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> SetSuspendedAsync(Guid id, bool suspended, long versionFila, Guid actorId, Guid? companyScope,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
