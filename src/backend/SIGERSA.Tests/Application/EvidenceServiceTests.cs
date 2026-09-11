using SIGERSA.Application.Evidences;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Storage;

namespace SIGERSA.Tests.Application;

public sealed class EvidenceServiceTests
{
    private const string ValidHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task UploadShouldValidateAssignmentBeforeSendingBytesToStorage()
    {
        var storage = new FakeStorage();
        var repository = new FakeEvidenceRepository { CanUpload = false };
        var service = new EvidenceService(storage, repository);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UploadAsync(
            Guid.NewGuid(), Guid.NewGuid(), "evidencias", "foto.png", "image/png",
            "FOTOGRAFIA", null, null, null, null,
            new MemoryStream([137, 80, 78, 71]), CancellationToken.None));

        Assert.False(storage.UploadCalled);
        Assert.Null(repository.Created);
    }

    [Fact]
    public async Task UploadShouldPersistOnlyStorageMetadataAndHash()
    {
        var evaluationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var storage = new FakeStorage();
        var repository = new FakeEvidenceRepository { CanUpload = true };
        var service = new EvidenceService(storage, repository);

        var evidence = await service.UploadAsync(
            evaluationId, userId, "evidencias", "inspeccion.png", "image/png",
            "FOTOGRAFIA", null, 18.4861, -69.9312, 12.5,
            new MemoryStream([137, 80, 78, 71]), CancellationToken.None);

        Assert.True(storage.UploadCalled);
        Assert.Same(evidence, repository.Created);
        Assert.Equal(evaluationId, evidence.EvaluationId);
        Assert.Equal(ValidHash, evidence.Sha256Hash);
        Assert.Equal(18.4861, evidence.Latitude);
        Assert.Equal(-69.9312, evidence.Longitude);
        Assert.StartsWith($"evaluaciones/{evaluationId:N}/", evidence.SupabasePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignedUploadShouldUseDeterministicPathAndVerifyRemoteBytesBeforePersisting()
    {
        var evaluationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();
        var storage = new FakeStorage();
        var repository = new FakeEvidenceRepository { CanUpload = true };
        var service = new EvidenceService(storage, repository);

        var authorization = await service.AuthorizeUploadAsync(
            evaluationId, userId, idempotencyKey, "evidencias", "foto.png",
            "image/png", 4, CancellationToken.None);
        var confirmed = await service.ConfirmUploadAsync(
            evaluationId, userId, idempotencyKey, "evidencias", authorization.SupabasePath,
            "foto.png", "image/png", "FOTOGRAFIA", 4, ValidHash, null,
            null, null, null, CancellationToken.None);

        Assert.Equal($"evaluaciones/{evaluationId:N}/{idempotencyKey:N}.png", authorization.SupabasePath);
        Assert.True(storage.VerificationCalled);
        Assert.Equal(idempotencyKey, confirmed.IdempotencyKey);
        Assert.Same(confirmed, repository.Created);
    }

    [Fact]
    public async Task ConfirmationShouldRejectMetadataThatDoesNotMatchRemoteObject()
    {
        var evaluationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();
        var service = new EvidenceService(
            new FakeStorage(), new FakeEvidenceRepository { CanUpload = true });

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ConfirmUploadAsync(
            evaluationId, userId, idempotencyKey, "evidencias",
            $"evaluaciones/{evaluationId:N}/{idempotencyKey:N}.png", "foto.png",
            "image/png", "FOTOGRAFIA", 5, ValidHash, null,
            null, null, null, CancellationToken.None));
    }

    private sealed class FakeStorage : IFileStorage
    {
        public bool UploadCalled { get; private set; }
        public bool VerificationCalled { get; private set; }

        public Task<StoredFile> UploadAsync(StorageUpload upload, CancellationToken cancellationToken = default)
        {
            UploadCalled = true;
            return Task.FromResult(new StoredFile(
                upload.BucketName, upload.SupabasePath, 4, upload.MimeType, ValidHash));
        }

        public Task<StorageUploadAuthorization> CreateUploadAuthorizationAsync(
            string bucketName, string supabasePath, string mimeType, long fileSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new StorageUploadAuthorization(bucketName, supabasePath, "token", "signed-url"));

        public Task<StoredFile> VerifyAsync(
            string bucketName, string supabasePath, string mimeType,
            CancellationToken cancellationToken = default)
        {
            VerificationCalled = true;
            return Task.FromResult(new StoredFile(bucketName, supabasePath, 4, mimeType, ValidHash));
        }

        public Task<Stream> DownloadAsync(string bucketName, string supabasePath, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream());
    }

    private sealed class FakeEvidenceRepository : IEvidenceRepository
    {
        public bool CanUpload { get; init; }
        public EvidenceRecord? Created { get; private set; }

        public Task<EvidencesPage> SearchAsync(EvidenceSearch query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EvidencesPage([], query.Page, query.PageSize, 0));

        public Task<EvidenceStorageReference?> GetAuthorizedAsync(
            Guid evidenceId, Guid actorId, Guid? companyScope, bool globalScope, bool assignedOnly,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EvidenceStorageReference?>(null);

        public Task<bool> CanUploadAsync(Guid userId, Guid evaluationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(CanUpload);

        public Task<Guid> CreateAsync(EvidenceRecord evidence, CancellationToken cancellationToken = default)
        {
            Created = evidence;
            return Task.FromResult(evidence.Id);
        }
    }
}
