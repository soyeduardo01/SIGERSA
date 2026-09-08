using SIGERSA.Infrastructure.Storage;

namespace SIGERSA.Tests.Infrastructure;

public sealed class FileSignatureValidatorTests
{
    [Fact]
    public void HasValidSignatureShouldAcceptPdfHeader()
    {
        byte[] content = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37];

        Assert.True(FileSignatureValidator.HasValidSignature("application/pdf", content));
    }

    [Fact]
    public void HasValidSignatureShouldRejectMimeMismatch()
    {
        byte[] content = [0xFF, 0xD8, 0xFF, 0xE0];

        Assert.False(FileSignatureValidator.HasValidSignature("image/png", content));
    }
}
