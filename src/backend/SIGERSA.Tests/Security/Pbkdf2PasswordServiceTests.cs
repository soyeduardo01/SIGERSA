using SIGERSA.Infrastructure.Security;

namespace SIGERSA.Tests.Security;

public sealed class BcryptPasswordServiceTests
{
    [Fact]
    public void HashShouldUseSaltAndVerifyOnlyTheOriginalSecret()
    {
        var service = new BcryptPasswordService();

        var first = service.Hash("Correct-Horse-2026!");
        var second = service.Hash("Correct-Horse-2026!");

        Assert.StartsWith("$2", first, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
        Assert.True(service.Verify(first, "Correct-Horse-2026!"));
        Assert.False(service.Verify(first, "Wrong-Password-2026!"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-supported-hash")]
    [InlineData("sigersa-pbkdf2-sha256$1$invalid$invalid")]
    public void VerifyShouldRejectMalformedOrWeakHashes(string hash)
    {
        Assert.False(new BcryptPasswordService().Verify(hash, "Secret-2026!"));
    }
}
