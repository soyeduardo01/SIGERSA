using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using SIGERSA.Application.Operations;
using SIGERSA.Domain.Entities;
using SIGERSA.Infrastructure.Reports;

namespace SIGERSA.Tests.Application;

public sealed class InstitutionalPdfRendererTests
{
    [Fact]
    public void HtmlTemplateEmbedsInstitutionalAssetsAndPoppins()
    {
        var html = InstitutionalReportHtmlBuilder.Build(SampleData(), true,
            new InstitutionalReportAssets(
                "data:image/png;base64,SIGERSA_LOGO",
                "data:image/png;base64,DIGEMAPS_LOGO",
                "data:font/ttf;base64,POPPINS_REGULAR",
                "data:font/ttf;base64,POPPINS_SEMIBOLD"));

        Assert.Contains("SIGERSA_LOGO", html, StringComparison.Ordinal);
        Assert.Contains("DIGEMAPS_LOGO", html, StringComparison.Ordinal);
        Assert.Contains("POPPINS_REGULAR", html, StringComparison.Ordinal);
        Assert.Contains("font-family:Poppins", html, StringComparison.Ordinal);
        Assert.Contains("Evaluación de Buenas", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChromiumRendererCanProduceQaPdfWhenRequested()
    {
        var output = Environment.GetEnvironmentVariable("SIGERSA_CHROMIUM_PDF_QA_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) return;

        var renderer = new ChromiumInstitutionalPdfRenderer(Options.Create(new ReportOptions
        {
            BrowserExecutablePath = Environment.GetEnvironmentVariable("CHROME_PATH")
        }));
        var pdf = await renderer.RenderAsync(SampleData(), true);

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4), StringComparison.Ordinal);
        Assert.True(pdf.Length > 100_000);
        await File.WriteAllBytesAsync(output, pdf);
    }

    private static ReportGenerationData SampleData() => new(
        Guid.NewGuid(), "EV-20260914-F902C514", "CAS-20260914-AC2F0665", "BEPENSA",
        "BEPENSA DOMINICANA", "Av. Independencia, Centro de los Héroes 02", "Patricia Mojica",
        "APROBADA", 90m, 1m, 1.54m, 1.54m, "BAJO", "ANUAL",
        DateTimeOffset.Parse("2026-09-14T13:00:00Z", CultureInfo.InvariantCulture),
        DateTimeOffset.Parse("2026-09-14T16:00:00Z", CultureInfo.InvariantCulture),
        [new ReportFinding("NC-001", "MENOR", "Registro de control pendiente de firma.", "ABIERTA")],
        [new ReportEvidence("evidencia-planta.jpg", "FOTOGRAFIA", "image/jpeg")]);
}
