using SIGERSA.Application.Documents;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Storage;

namespace SIGERSA.Tests.Application;

public sealed class SupportingDocumentServiceTests
{
    [Fact]
    public async Task RequestDocumentPersistsVerifiedStorageMetadata()
    {
        var repository = new FakeRepository { CanAttachRequest = true };
        var storage = new FakeStorage();
        var service = new SupportingDocumentService(repository, storage);
        var actor = new DocumentActor(Guid.NewGuid(), ["USUARIO_DELEGADO"], Guid.NewGuid());

        var id = await service.UploadRequestDocumentAsync(
            Guid.NewGuid(), "Registro sanitario", true, "documentos", "registro.pdf",
            "application/pdf", 4, new MemoryStream([1, 2, 3, 4]), actor, CancellationToken.None);

        Assert.Equal(repository.Saved?.Id, id);
        Assert.Equal("REGISTRO SANITARIO", repository.DocumentType);
        Assert.True(repository.Required);
        Assert.Equal("hash-seguro", repository.Saved?.Sha256Hash);
        Assert.True(storage.Uploaded);
    }

    [Fact]
    public async Task AuthorizationLetterRequiresAuthorizedScopeBeforeUploading()
    {
        var repository = new FakeRepository { CanAttachUser = false };
        var storage = new FakeStorage();
        var service = new SupportingDocumentService(repository, storage);
        var actor = new DocumentActor(Guid.NewGuid(), ["ADMINISTRADOR_EMPRESA"], Guid.NewGuid());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UploadUserAuthorizationAsync(
            Guid.NewGuid(), "documentos", "carta.png", "image/png", 4,
            new MemoryStream([1, 2, 3, 4]), actor, CancellationToken.None));

        Assert.False(storage.Uploaded);
    }

    [Fact]
    public async Task RejectsUnsupportedOrOversizedDocuments()
    {
        var service = new SupportingDocumentService(new FakeRepository(), new FakeStorage());
        var actor = new DocumentActor(Guid.NewGuid(), ["ADMINISTRADOR"], null);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.UploadUserAuthorizationAsync(
            Guid.NewGuid(), "documentos", "carta.exe", "application/octet-stream", 4,
            new MemoryStream([1, 2, 3, 4]), actor, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.UploadUserAuthorizationAsync(
            Guid.NewGuid(), "documentos", "carta.pdf", "application/pdf", 5 * 1024 * 1024 + 1,
            new MemoryStream([1]), actor, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsRoleWithoutDocumentPermission()
    {
        var service = new SupportingDocumentService(new FakeRepository(), new FakeStorage());
        var actor = new DocumentActor(Guid.NewGuid(), ["TECNICO_EVALUADOR"], null);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.UploadRequestDocumentAsync(
            Guid.NewGuid(), "SOPORTE", true, "documentos", "soporte.pdf", "application/pdf",
            4, new MemoryStream([1, 2, 3, 4]), actor, CancellationToken.None));
    }

    private sealed class FakeStorage : IFileStorage
    {
        public bool Uploaded { get; private set; }
        public Task<StoredFile> UploadAsync(StorageUpload upload, CancellationToken cancellationToken = default)
        {
            Uploaded = true;
            return Task.FromResult(new StoredFile(
                upload.BucketName, upload.SupabasePath, 4, upload.MimeType, "hash-seguro"));
        }
        public Task<StorageUploadAuthorization> CreateUploadAuthorizationAsync(
            string bucketName, string supabasePath, string mimeType, long fileSize,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StoredFile> VerifyAsync(string bucketName, string supabasePath, string mimeType,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> DownloadAsync(string bucketName, string supabasePath,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeRepository : ISupportingDocumentRepository
    {
        public bool CanAttachUser { get; init; }
        public bool CanAttachRequest { get; init; }
        public SupportingDocument? Saved { get; private set; }
        public string? DocumentType { get; private set; }
        public bool Required { get; private set; }

        public Task<bool> CanAttachToUserAsync(Guid targetUserId, Guid? companyScope, bool globalScope,
            CancellationToken cancellationToken = default) => Task.FromResult(CanAttachUser);
        public Task<bool> CanAttachToRequestAsync(Guid requestId, Guid actorId, Guid? companyScope,
            bool globalScope, CancellationToken cancellationToken = default) => Task.FromResult(CanAttachRequest);
        public Task<Guid> SaveUserAuthorizationAsync(Guid userId, SupportingDocument document, Guid actorId,
            CancellationToken cancellationToken = default)
        {
            Saved = document;
            return Task.FromResult(document.Id);
        }
        public Task<Guid> SaveRequestDocumentAsync(Guid requestId, string documentType, bool required,
            SupportingDocument document, Guid actorId, CancellationToken cancellationToken = default)
        {
            Saved = document;
            DocumentType = documentType;
            Required = required;
            return Task.FromResult(document.Id);
        }
        public Task<SupportingDocumentReference?> GetUserAuthorizationAsync(Guid userId, Guid? companyScope,
            bool globalScope, CancellationToken cancellationToken = default) =>
            Task.FromResult<SupportingDocumentReference?>(null);
    }
}
