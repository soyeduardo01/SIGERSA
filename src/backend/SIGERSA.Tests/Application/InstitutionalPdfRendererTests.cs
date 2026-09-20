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
                "data:font/ttf;base64,POPPINS_REGULAR",
                "data:font/ttf;base64,POPPINS_SEMIBOLD"),
            "https://sigersa.example/api/v1/reports/verify/0123456789abcdef0123456789abcdef0123456789abcdef");

        Assert.Contains("SIGERSA_LOGO", html, StringComparison.Ordinal);
        Assert.DoesNotContain("DIGEMAPS_LOGO", html, StringComparison.Ordinal);
        Assert.Contains("POPPINS_REGULAR", html, StringComparison.Ordinal);
        Assert.Contains("font-family:Poppins", html, StringComparison.Ordinal);
        Assert.Contains("Evaluación de Buenas", html, StringComparison.Ordinal);
        Assert.Contains("Escala de clasificación de riesgo total", html, StringComparison.Ordinal);
        Assert.Contains("Bajo (1.0-3.6)", html, StringComparison.Ordinal);
        Assert.Contains("Medio (&gt;3.6-6.3)", html, StringComparison.Ordinal);
        Assert.Contains("Alto (&gt;6.3-9.0)", html, StringComparison.Ordinal);
        Assert.Contains("Flujo de decisión sanitaria", html, StringComparison.Ordinal);
        Assert.Contains("Código QR de verificación", html, StringComparison.Ordinal);
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
        var data = Environment.GetEnvironmentVariable("SIGERSA_PDF_QA_LARGE") == "1"
            ? LargeSampleData()
            : SampleData();
        if (Environment.GetEnvironmentVariable("SIGERSA_PDF_QA_REJECTED") == "1")
            data = data with { Status = "NO_APROBADA" };
        var pdf = await renderer.RenderAsync(data, true,
            "https://sigersa.example/api/v1/reports/verify/0123456789abcdef0123456789abcdef0123456789abcdef");

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
        [new ReportEvidence("evidencia-planta.jpg", "FOTOGRAFIA", "image/jpeg")],
        [new EvaluationFollowUpItem("Completar y firmar el registro de control.", new DateOnly(2026, 9, 30))],
        [new EvaluationFollowUpItem("Verificar el registro en la próxima inspección.", null)]);

    private static ReportGenerationData LargeSampleData() => SampleData() with
    {
        Findings = Enumerable.Range(1, 48).Select(index => new ReportFinding(
            $"NC-{index:000}", index % 5 == 0 ? "CRÍTICA" : "MAYOR",
            $"Hallazgo extenso {index}: se requiere documentar la corrección, verificar la evidencia y conservar la trazabilidad del seguimiento sin invadir el pie de página.",
            "ABIERTA")).ToArray(),
        Evidences = Enumerable.Range(1, 32).Select(index => new ReportEvidence(
            $"evidencia-de-verificacion-{index:000}.jpg", "FOTOGRAFIA", "image/jpeg")).ToArray()
    };
}
