using SIGERSA.Application.Operations;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Storage;

namespace SIGERSA.Tests.Application;

public sealed class ReportServiceTests
{
    [Theory]
    [InlineData("APROBADA", true)]
    [InlineData("NO_APROBADA", true)]
    [InlineData("EN_REVISION", false)]
    [InlineData("CERRADA", false)]
    public void OfficialReportRequiresFinalApprovalDecision(string status, bool expected)
    {
        Assert.Equal(expected, ReportService.CanIssueOfficialReport(status));
    }

    [Fact]
    public async Task MissingEvidenceDoesNotPreventBuildingOfficialReportAttachments()
    {
        var available = new ReportEvidence(
            "disponible.png", "FOTOGRAFIA", "image/png", "SIGERSA_FILES", "evaluaciones/disponible.png");
        var missing = new ReportEvidence(
            "eliminada.png", "FOTOGRAFIA", "image/png", "SIGERSA_FILES", "evaluaciones/eliminada.png");
        var storage = new FakeStorage(missing.SupabasePath!);

        var attachments = await ReportService.DownloadAvailableAttachmentsAsync(
            [available, missing], storage, CancellationToken.None, maxAttempts: 2,
            delay: static (_, _) => Task.CompletedTask);

        var attachment = Assert.Single(attachments);
        Assert.Equal(available.Name, attachment.Name);
        Assert.Equal([137, 80, 78, 71], attachment.Content);
    }

    [Fact]
    public async Task EvidenceDownloadWaitsForSupabaseAndRetriesBeforeRendering()
    {
        var evidence = new ReportEvidence(
            "reciente.png", "FOTOGRAFIA", "image/png",
            "SIGERSA_FILES", "evaluaciones/reciente.png");
        var storage = new FakeStorage(failuresBeforeSuccess: 2);
        var waits = new List<TimeSpan>();

        var attachments = await ReportService.DownloadAvailableAttachmentsAsync(
            [evidence], storage, CancellationToken.None, maxAttempts: 4,
            delay: (duration, _) =>
            {
                waits.Add(duration);
                return Task.CompletedTask;
            });

        Assert.Single(attachments);
        Assert.Equal(3, storage.DownloadAttempts);
        Assert.Equal([TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1)], waits);
    }

    private sealed class FakeStorage(string? missingPath = null, int failuresBeforeSuccess = 0) : IFileStorage
    {
        public int DownloadAttempts { get; private set; }

        public Task<Stream> DownloadAsync(
            string bucketName, string supabasePath, CancellationToken cancellationToken = default)
        {
            DownloadAttempts++;
            if (supabasePath == missingPath)
                throw new FileNotFoundException("Objeto remoto eliminado.", supabasePath);
            if (DownloadAttempts <= failuresBeforeSuccess)
                throw new FileNotFoundException("Objeto todavía no visible.", supabasePath);
            return Task.FromResult<Stream>(new MemoryStream([137, 80, 78, 71]));
        }

        public Task<StoredFile> UploadAsync(StorageUpload upload, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StorageUploadAuthorization> CreateUploadAuthorizationAsync(
            string bucketName, string supabasePath, string mimeType, long fileSize,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<StoredFile> VerifyAsync(
            string bucketName, string supabasePath, string mimeType,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
