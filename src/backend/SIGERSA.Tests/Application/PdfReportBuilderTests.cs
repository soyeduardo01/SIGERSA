using System.Text;
using SIGERSA.Application.Operations;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Tests.Application;

public sealed class PdfReportBuilderTests
{
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
            findings, evidences);

        var pdf = PdfReportBuilder.Build(data, true);
        var text = Encoding.Latin1.GetString(pdf);

        Assert.StartsWith("%PDF-1.4", text, StringComparison.Ordinal);
        Assert.Contains("NC-090", text, StringComparison.Ordinal);
        Assert.Contains("evidencia-040.jpg", text, StringComparison.Ordinal);
        Assert.True(Count(text, "/Type /Page ") >= 3);
    }

    private static int Count(string value, string fragment)
    {
        var total = 0;
        for (var index = 0; (index = value.IndexOf(fragment, index, StringComparison.Ordinal)) >= 0; index += fragment.Length)
            total++;
        return total;
    }
}
