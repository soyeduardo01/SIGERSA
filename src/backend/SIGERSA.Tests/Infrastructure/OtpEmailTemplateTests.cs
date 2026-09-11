using SIGERSA.Infrastructure.Email;

namespace SIGERSA.Tests.Infrastructure;

public sealed class OtpEmailTemplateTests
{
    [Fact]
    public void RecoveryTemplateIncludesBrandCodeAndSafeContent()
    {
        var html = OtpEmailTemplate.BuildHtml(
            "Ana <Pérez>", "754273",
            new DateTimeOffset(2026, 9, 14, 2, 45, 0, TimeSpan.Zero),
            "Código de recuperación",
            "Hemos recibido una solicitud para restablecer tu contraseña.",
            "Restablecer contraseña",
            "https://sigersa.example/recuperar-clave.html");

        Assert.Contains("SIGERSA", html, StringComparison.Ordinal);
        Assert.Contains("754273", html, StringComparison.Ordinal);
        Assert.Contains(System.Net.WebUtility.HtmlEncode("Restablecer contraseña"), html, StringComparison.Ordinal);
        Assert.Contains("https://sigersa.example/recuperar-clave.html", html, StringComparison.Ordinal);
        Assert.Contains(System.Net.WebUtility.HtmlEncode("Ana <Pérez>"), html, StringComparison.Ordinal);
        Assert.DoesNotContain("Ana <Pérez>", html, StringComparison.Ordinal);
        Assert.Contains("background:#07523e", html, StringComparison.Ordinal);
        Assert.Contains("Mensaje automático de SIGERSA", html, StringComparison.Ordinal);
        Assert.DoesNotContain("También puedes usar el código manualmente", html, StringComparison.Ordinal);
    }
}
