using SIGERSA.Application.Evidences;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Storage;

namespace SIGERSA.Tests.Application;

public sealed class EvidenceServiceTests
{
    [Fact]
    public async Task UploadShouldValidateAssignmentBeforeSendingBytesToStorage()
    {
        var storage = new FakeStorage();
        var repository = new FakeEvidenceRepository { CanUpload = false };
        var service = new EvidenceService(storage, repository);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UploadAsync(
            Guid.NewGuid(), Guid.NewGuid(), "evidencias", "foto.png", "image/png",
            "FOTOGRAFIA", new MemoryStream([137, 80, 78, 71]), CancellationToken.None));

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
            "FOTOGRAFIA", new MemoryStream([137, 80, 78, 71]), CancellationToken.None);

        Assert.True(storage.UploadCalled);
        Assert.Same(evidence, repository.Created);
        Assert.Equal(evaluationId, evidence.EvaluationId);
        Assert.Equal("abc123", evidence.Sha256Hash);
        Assert.StartsWith($"evaluaciones/{evaluationId:N}/", evidence.SupabasePath, StringComparison.Ordinal);
    }

    private sealed class FakeStorage : IFileStorage
    {
        public bool UploadCalled { get; private set; }

        public Task<StoredFile> UploadAsync(StorageUpload upload, CancellationToken cancellationToken = default)
        {
            UploadCalled = true;
            return Task.FromResult(new StoredFile(
                upload.BucketName, upload.SupabasePath, 4, upload.MimeType, "abc123"));
        }

        public Task<Stream> DownloadAsync(string bucketName, string supabasePath, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream());
    }

    private sealed class FakeEvidenceRepository : IEvidenceRepository
    {
        public bool CanUpload { get; init; }
        public EvidenceRecord? Created { get; private set; }

        public Task<bool> CanUploadAsync(Guid userId, Guid evaluationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(CanUpload);

        public Task<Guid> CreateAsync(EvidenceRecord evidence, CancellationToken cancellationToken = default)
        {
            Created = evidence;
            return Task.FromResult(evidence.Id);
        }
    }
}
