using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Storage;

namespace SIGERSA.Application.Operations;

public sealed class ReportService(
    IOperationalRepository repository,
    IFileStorage storage,
    IOptions<ReportOptions> options)
{
    private readonly ReportOptions _options = options.Value;

    public async Task<ReportFileReference> GenerateAsync(
        Guid evaluationId,
        bool official,
        OperationalActor actor,
        CancellationToken cancellationToken)
    {
        EnsureReviewer(actor);
        var data = await repository.GetReportDataAsync(evaluationId, actor.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("La evaluación no existe o todavía no está lista para generar su informe.");
        var mainReport = InstitutionalPdfReportBuilder.Build(data, official);
        var attachments = new List<ReportAttachment>();
        foreach (var evidence in data.Evidences)
        {
            if (string.IsNullOrWhiteSpace(evidence.BucketName) || string.IsNullOrWhiteSpace(evidence.SupabasePath))
                continue;
            await using var evidenceStream = await storage.DownloadAsync(
                evidence.BucketName, evidence.SupabasePath, cancellationToken);
            using var buffer = new MemoryStream();
            await evidenceStream.CopyToAsync(buffer, cancellationToken);
            attachments.Add(new ReportAttachment(evidence.Name, evidence.MimeType, buffer.ToArray()));
        }
        var content = PdfAttachmentMerger.Merge(mainReport, attachments);
        var path = $"informes/{evaluationId:N}/{Guid.NewGuid():N}.pdf";
        await using var stream = new MemoryStream(content, writable: false);
        var stored = await storage.UploadAsync(new StorageUpload(
            _options.BucketName, path, "application/pdf", stream), cancellationToken);
        return await repository.SaveReportVersionAsync(
            evaluationId, stored.BucketName, stored.SupabasePath, stored.FileSize,
            stored.Sha256Hash, official, actor.UserId, cancellationToken);
    }

    public async Task<ReportDownload> DownloadAsync(
        Guid reportId,
        OperationalActor actor,
        CancellationToken cancellationToken)
    {
        var scope = Scope(actor);
        var reference = await repository.GetReportFileAsync(reportId, scope, cancellationToken)
            ?? throw new KeyNotFoundException("El informe no existe o no está disponible para el usuario.");
        var content = await storage.DownloadAsync(reference.BucketName, reference.SupabasePath, cancellationToken);
        return new ReportDownload(content, reference.MimeType, reference.FileName);
    }

    private static OperationalActorScope Scope(OperationalActor actor)
    {
        var global = actor.Roles.Any(role => role is "ADMINISTRADOR" or "COORDINADOR");
        var assigned = !global && actor.Roles.Contains("TECNICO_EVALUADOR", StringComparer.Ordinal);
        Guid? company = global || assigned ? null : actor.CompanyId
            ?? throw new ForbiddenException("El usuario no tiene un ámbito empresarial válido.");
        return new OperationalActorScope(actor.UserId, company, global, assigned,
            actor.Roles.Contains("USUARIO_DELEGADO", StringComparer.Ordinal));
    }

    private static void EnsureReviewer(OperationalActor actor)
    {
        if (!actor.Roles.Any(role => role is "ADMINISTRADOR" or "COORDINADOR"))
            throw new ForbiddenException("Solo un coordinador autorizado puede generar o emitir informes.");
    }
}

public sealed record ReportDownload(Stream Content, string MimeType, string FileName);

internal static class PdfReportBuilder
{
    public static byte[] Build(ReportGenerationData data, bool official)
    {
        var lines = new List<string>
        {
            "SIGERSA - INFORME DE EVALUACIÓN BPM",
            official ? "INFORME OFICIAL" : "BORRADOR PARA REVISIÓN",
            $"Evaluación: {data.EvaluationNumber}",
            $"Caso: {data.CaseNumber}",
            $"Empresa: {data.CompanyName}",
            $"Establecimiento: {data.EstablishmentName}",
            $"Dirección: {data.Address}",
            $"Técnico evaluador: {data.EvaluatorName}",
            $"Ejecución: {FormatDate(data.StartedAt)} - {FormatDate(data.FinishedAt)}",
            "",
            "RESUMEN EJECUTIVO",
            $"Cumplimiento BPM: {FormatNumber(data.CompliancePercentage, "%")}",
            $"Riesgo del producto: {FormatNumber(data.ProductRisk)}",
            $"Riesgo del establecimiento: {FormatNumber(data.EstablishmentRisk)}",
            $"Riesgo total: {FormatNumber(data.TotalRisk)}",
            $"Clasificación: {data.RiskLevel ?? "No calculable"}",
            $"Frecuencia recomendada: {data.Frequency ?? "No aplica"}",
            "",
            "HALLAZGOS Y NO CONFORMIDADES"
        };
        if (data.Findings.Count == 0) lines.Add("No se registraron no conformidades.");
        foreach (var finding in data.Findings)
            lines.Add($"{finding.Code} [{finding.Criticality}] {finding.Description} ({finding.Status})");
        lines.Add("");
        lines.Add("RECOMENDACIONES");
        lines.Add(data.Findings.Count == 0
            ? "Mantener los controles BPM y la frecuencia de inspección determinada."
            : "Corregir las no conformidades dentro de los plazos asignados y conservar evidencia verificable.");
        lines.Add("");
        lines.Add("ANEXO DE EVIDENCIAS");
        if (data.Evidences.Count == 0) lines.Add("No se adjuntaron evidencias.");
        foreach (var evidence in data.Evidences)
            lines.Add($"{evidence.Type}: {evidence.Name} ({evidence.MimeType})");
        lines.Add("");
        lines.Add($"Generado por SIGERSA el {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC.");
        return BuildPages(lines.SelectMany(Wrap).ToArray());
    }

    private static IEnumerable<string> Wrap(string value)
    {
        if (value.Length <= 92) return [value];
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        var current = new StringBuilder();
        foreach (var word in words)
        {
            if (current.Length > 0 && current.Length + word.Length + 1 > 92)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }
        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }

    private static string FormatDate(DateTimeOffset? value) =>
        value?.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) ?? "No registrada";

    private static string FormatNumber(decimal? value, string suffix = "") =>
        value.HasValue ? value.Value.ToString("0.00", CultureInfo.InvariantCulture) + suffix : "No calculable";

    private static byte[] BuildPages(IReadOnlyList<string> lines)
    {
        const int linesPerPage = 52;
        var pages = lines.Chunk(linesPerPage).ToArray();
        if (pages.Length == 0) pages = [[]];
        var kids = string.Join(' ', Enumerable.Range(0, pages.Length).Select(index => $"{4 + index * 2} 0 R"));
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{kids}] /Count {pages.Length} >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"
        };
        for (var index = 0; index < pages.Length; index++)
        {
            var pageNumber = 4 + index * 2;
            var contentNumber = pageNumber + 1;
            var content = new StringBuilder("BT\n/F1 10 Tf\n48 790 Td\n13 TL\n");
            foreach (var line in pages[index])
                content.Append('(').Append(Escape(line)).Append(") Tj\nT*\n");
            content.Append("ET\n");
            var streamText = content.ToString();
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentNumber} 0 R >>");
            objects.Add($"<< /Length {Encoding.Latin1.GetByteCount(streamText)} >>\nstream\n{streamText}endstream");
        }
        using var output = new MemoryStream();
        Write(output, "%PDF-1.4\n%âãÏÓ\n");
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(output.Position);
            Write(output, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        var xref = output.Position;
        Write(output, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Write(output, $"{offset:0000000000} 00000 n \n");
        Write(output, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);

    private static void Write(Stream stream, string value)
    {
        var bytes = Encoding.Latin1.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}
