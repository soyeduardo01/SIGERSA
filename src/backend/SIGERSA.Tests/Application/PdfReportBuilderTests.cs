using System.Text;
using System.Globalization;
using Microsoft.Extensions.Options;
using PdfSharp.Pdf.IO;
using SIGERSA.Application.Operations;
using SIGERSA.Domain.Entities;
using SIGERSA.Infrastructure.Reports;

namespace SIGERSA.Tests.Application;

public sealed class PdfReportBuilderTests
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
    public void RejectedReportShowsStampAndSavedFollowUps()
    {
        var data = SampleData() with { Status = "NO_APROBADA" };
        var html = InstitutionalReportHtmlBuilder.Build(data, true,
            new InstitutionalReportAssets("logo", "regular", "semibold"),
            "https://sigersa.example/api/v1/reports/verify/0123456789abcdef0123456789abcdef0123456789abcdef");

        Assert.Contains("EVALUACIÓN NO APROVADA", html, StringComparison.Ordinal);
        Assert.Contains("Completar y firmar el registro de control.", html, StringComparison.Ordinal);
        Assert.Contains("30/09/2026", html, StringComparison.Ordinal);
        Assert.Contains("Verificar el registro en la pr&#243;xima inspecci&#243;n.", html, StringComparison.Ordinal);
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
        var pdf = await renderer.RenderAsync(SampleData(), true,
            "https://sigersa.example/api/v1/reports/verify/0123456789abcdef0123456789abcdef0123456789abcdef");

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4), StringComparison.Ordinal);
        Assert.True(pdf.Length > 100_000);
        await File.WriteAllBytesAsync(output, pdf);
    }

    [Fact]
    public void InstitutionalReportContainsCompleteVisualStructure()
    {
        var data = new ReportGenerationData(
            Guid.NewGuid(), "EV-20260914-F902C514", "CAS-20260914-AC2F0665", "BEPENSA",
            "BEPENSA DOMINICANA", "Av. Independencia, Centro de los Héroes 02", "Patricia Mojica",
            "APROBADA", 90m, 1m, 1.54m, 1.54m, "BAJO", "ANUAL",
            DateTimeOffset.Parse("2026-09-14T13:00:00Z", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2026-09-14T16:00:00Z", CultureInfo.InvariantCulture),
            [new ReportFinding("NC-001", "MENOR", "Registro de control pendiente de firma.", "ABIERTA")],
            [new ReportEvidence("evidencia-planta.jpg", "FOTOGRAFIA", "image/jpeg")],
            [new EvaluationFollowUpItem("Completar y firmar el registro de control.", new DateOnly(2026, 9, 30))],
            [new EvaluationFollowUpItem("Verificar el registro en la próxima inspección.", null)]);

        var pdf = InstitutionalPdfReportBuilder.Build(data, true);
        using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);

        Assert.True(document.PageCount >= 5);
        Assert.True(pdf.Length > 20_000);
        var qaOutput = Environment.GetEnvironmentVariable("SIGERSA_PDF_QA_OUTPUT");
        if (!string.IsNullOrWhiteSpace(qaOutput)) File.WriteAllBytes(qaOutput, pdf);
    }

    [Fact]
    public void IncludesEveryFindingAndEvidenceAcrossMultiplePages()
    {
        var findings = Enumerable.Range(1, 90)
            .Select(index => new ReportFinding($"NC-{index:000}", "ALTA", $"Hallazgo verificable {index}", "ABIERTA"))
            .ToArray();
        var evidences = Enumerable.Range(1, 40)
            .Select(index => new ReportEvidence($"evidencia-{index:000}.jpg", "FOTOGRAFIA", "image/jpeg"))
            .ToArray();
        var data = new ReportGenerationData(
            Guid.NewGuid(), "EVA-2026-001", "CAS-2026-001", "Empresa", "Planta",
            "Dirección", "Técnico", "APROBADA", 95m, 1m, 2m, 3m,
            "BAJO", "ANUAL", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            findings, evidences, [], []);

        var pdf = PdfReportBuilder.Build(data, true);
        var text = Encoding.Latin1.GetString(pdf);

        Assert.StartsWith("%PDF-1.4", text, StringComparison.Ordinal);
        Assert.Contains("NC-090", text, StringComparison.Ordinal);
        Assert.Contains("evidencia-040.jpg", text, StringComparison.Ordinal);
        Assert.True(Count(text, "/Type /Page ") >= 3);
    }

    [Fact]
    public void AttachmentMergerKeepsValidationPageAtTheEnd()
    {
        static byte[] Pdf(params PdfSharp.PageSize[] sizes)
        {
            using var document = new PdfSharp.Pdf.PdfDocument();
            foreach (var size in sizes) document.AddPage().Size = size;
            using var stream = new MemoryStream();
            document.Save(stream, closeStream: false);
            return stream.ToArray();
        }

        var main = Pdf(PdfSharp.PageSize.Letter, PdfSharp.PageSize.Letter);
        var attachment = new ReportAttachment("anexo.pdf", "application/pdf", Pdf(PdfSharp.PageSize.A4));
        var merged = PdfAttachmentMerger.Merge(main, [attachment]);
        using var result = PdfReader.Open(new MemoryStream(merged), PdfDocumentOpenMode.Import);

        Assert.Equal(3, result.PageCount);
        Assert.InRange(result.Pages[1].Width.Point, 595, 596);
        Assert.InRange(result.Pages[2].Width.Point, 611, 613);
    }

    private static int Count(string value, string fragment)
    {
        var total = 0;
        for (var index = 0; (index = value.IndexOf(fragment, index, StringComparison.Ordinal)) >= 0; index += fragment.Length)
            total++;
        return total;
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
}
