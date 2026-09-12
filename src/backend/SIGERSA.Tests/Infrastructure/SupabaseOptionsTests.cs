using System.Text;
using SIGERSA.Infrastructure.Configuration;

namespace SIGERSA.Tests.Infrastructure;

public sealed class SupabaseOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short-placeholder")]
    [InlineData("sb_publishable_example-key-that-is-not-for-the-server")]
    public void ServerCredentialRejectsMissingPlaceholderAndPublishableKeys(string? key) =>
        Assert.False(SupabaseOptions.IsServerCredential(key));

    [Fact]
    public void ServerCredentialAcceptsSecretKeyFormat() =>
        Assert.True(SupabaseOptions.IsServerCredential($"sb_secret_{new string('a', 32)}"));

    [Theory]
    [InlineData("service_role", true)]
    [InlineData("anon", false)]
    public void ServerCredentialOnlyAcceptsLegacyServiceRoleJwt(string role, bool expected)
    {
        var header = Base64Url("{\"alg\":\"HS256\"}");
        var payload = Base64Url($"{{\"role\":\"{role}\"}}");

        Assert.Equal(expected, SupabaseOptions.IsServerCredential($"{header}.{payload}.signature"));
    }

    private static string Base64Url(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}
