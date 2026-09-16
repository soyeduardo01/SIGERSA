using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Application.Operations;

internal sealed record ReportAttachment(string Name, string MimeType, byte[] Content);

internal static class InstitutionalPdfReportBuilder
{
    private static readonly XColor DarkGreen = XColor.FromArgb(20, 86, 66);
    private static readonly XColor MidGreen = XColor.FromArgb(34, 118, 88);
    private static readonly XColor Lime = XColor.FromArgb(170, 214, 105);
    private static readonly XColor Ink = XColor.FromArgb(40, 57, 53);

    public static byte[] Build(ReportGenerationData data, bool official)
    {
        EnsureFonts();
        using var document = new PdfDocument();
        document.Info.Title = $"Informe de Evaluación BPM - {data.EvaluationNumber}";
        DrawCover(document.AddPage(), data, official);
        DrawBody(document, data);
        using var output = new MemoryStream();
        document.Save(output, closeStream: false);
        return output.ToArray();
    }

    private static void DrawCover(PdfPage page, ReportGenerationData data, bool official)
    {
        page.Size = PdfSharp.PageSize.Letter;
        using var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawRectangle(new XSolidBrush(DarkGreen), 0, 0, page.Width.Point, page.Height.Point);
        gfx.DrawEllipse(new XSolidBrush(XColor.FromArgb(28, 111, 83)), -170, -210, 470, 360);
        gfx.DrawEllipse(new XSolidBrush(XColor.FromArgb(53, 137, 108)), 350, 610, 360, 360);
        var white = XBrushes.White;
        var muted = new XSolidBrush(XColor.FromArgb(220, 239, 232));
        gfx.DrawString("SIGERSA", new XFont("Arial", 34, XFontStyleEx.Bold), white, 48, 76);
        gfx.DrawString(official ? "INFORME OFICIAL DE AUDITORÍA SANITARIA" : "BORRADOR PARA REVISIÓN",
            new XFont("Arial", 9, XFontStyleEx.Bold), new XSolidBrush(Lime), 50, 176);
        var formatter = new XTextFormatter(gfx);
        formatter.DrawString("Evaluación de Buenas\nPrácticas de Manufactura (BPM)",
            new XFont("Arial", 31, XFontStyleEx.Bold), white, new XRect(48, 215, 500, 120));
        formatter.DrawString("Reporte técnico de cumplimiento normativo, gestión de riesgo sanitario y trazabilidad.",
            new XFont("Arial", 12), muted, new XRect(50, 350, 500, 55));
        DrawCard(gfx, 48, 435, 160, "N.º DE EVALUACIÓN", data.EvaluationNumber);
        DrawCard(gfx, 218, 435, 160, "N.º DE CASO", data.CaseNumber);
        DrawCard(gfx, 388, 435, 160, "CLASIFICACIÓN", data.RiskLevel ?? "NO CALCULABLE");
        gfx.DrawRoundedRectangle(XBrushes.White, 48, 530, 500, 150, 12, 12);
        DrawLabelValue(gfx, 72, 562, "EMPRESA", data.CompanyName);
        DrawLabelValue(gfx, 310, 562, "ESTABLECIMIENTO", data.EstablishmentName);
        DrawLabelValue(gfx, 72, 625, "TÉCNICO EVALUADOR", data.EvaluatorName);
        DrawLabelValue(gfx, 310, 625, "CUMPLIMIENTO BPM", Number(data.CompliancePercentage, "%"));
        gfx.DrawString("DIGEMAPS · Sistema Integral de Gestión de Riesgo y Seguridad Alimentaria",
            new XFont("Arial", 8), muted, 48, 760);
    }

    private static void DrawBody(PdfDocument document, ReportGenerationData data)
    {
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.Letter;
        var gfx = XGraphics.FromPdfPage(page);
        var y = 42d;
        Header(gfx, data, 2);
        y = Section(gfx, y + 48, "1", "Información general");
        y = Paragraph(gfx, y, $"Empresa: {data.CompanyName}\nEstablecimiento: {data.EstablishmentName}\nDirección: {data.Address}\nTécnico evaluador: {data.EvaluatorName}");
        y = Section(gfx, y + 8, "2", "Resumen ejecutivo");
        y = Paragraph(gfx, y,
            $"Cumplimiento BPM: {Number(data.CompliancePercentage, "%")}   |   Riesgo total: {Number(data.TotalRisk)}   |   Clasificación: {data.RiskLevel ?? "No calculable"}\nFrecuencia recomendada: {data.Frequency ?? "No aplica"}");
        y = Section(gfx, y + 8, "3", "Hallazgos y no conformidades");
        if (data.Findings.Count == 0) y = Paragraph(gfx, y, "No se registraron no conformidades.");
        foreach (var finding in data.Findings)
        {
            if (y > 700) { gfx.Dispose(); page = document.AddPage(); gfx = XGraphics.FromPdfPage(page); Header(gfx, data, document.PageCount); y = 92; }
            y = Paragraph(gfx, y, $"{finding.Code} · {finding.Criticality} · {finding.Description} ({finding.Status})", 9);
        }
        y = Section(gfx, y + 8, "4", "Listado de anexos");
        if (data.Evidences.Count == 0) y = Paragraph(gfx, y, "No se adjuntaron evidencias.");
        for (var index = 0; index < data.Evidences.Count; index++)
        {
            if (y > 705) { gfx.Dispose(); page = document.AddPage(); gfx = XGraphics.FromPdfPage(page); Header(gfx, data, document.PageCount); y = 92; }
            var evidence = data.Evidences[index];
            y = Paragraph(gfx, y, $"Anexo {index + 1:00} · {evidence.Name} · {evidence.MimeType}", 9);
        }
        y = Section(gfx, y + 8, "5", "Validación y trazabilidad");
        _ = Paragraph(gfx, y, $"Documento generado por SIGERSA el {DateTimeOffset.UtcNow:dd/MM/yyyy HH:mm} UTC. Identificador: {data.CaseNumber} · {data.EvaluationNumber}", 9);
        gfx.Dispose();
    }

    private static void Header(XGraphics gfx, ReportGenerationData data, int pageNumber)
    {
        gfx.DrawString("INFORME DE EVALUACIÓN BPM", new XFont("Arial", 13, XFontStyleEx.Bold), new XSolidBrush(DarkGreen), 42, 38);
        gfx.DrawString($"{data.EvaluationNumber} · {data.CaseNumber}", new XFont("Arial", 8), XBrushes.Gray, 42, 54);
        gfx.DrawString($"Página {pageNumber}", new XFont("Arial", 8), XBrushes.Gray, 520, 44);
        gfx.DrawLine(new XPen(MidGreen, 1), 42, 66, 570, 66);
    }

    private static double Section(XGraphics gfx, double y, string number, string title)
    {
        gfx.DrawEllipse(new XSolidBrush(MidGreen), 42, y - 12, 24, 24);
        gfx.DrawString(number, new XFont("Arial", 10, XFontStyleEx.Bold), XBrushes.White, new XRect(42, y - 10, 24, 20), XStringFormats.Center);
        gfx.DrawString(title, new XFont("Arial", 15, XFontStyleEx.Bold), new XSolidBrush(DarkGreen), 76, y + 3);
        return y + 24;
    }

    private static double Paragraph(XGraphics gfx, double y, string text, double size = 10)
    {
        var lines = text.Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length / 92d)));
        var height = lines * (size + 5) + 10;
        new XTextFormatter(gfx).DrawString(text, new XFont("Arial", size), new XSolidBrush(Ink), new XRect(48, y, 510, height));
        return y + height;
    }

    private static void DrawCard(XGraphics gfx, double x, double y, double width, string label, string value)
    {
        gfx.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(45, 122, 94)), x, y, width, 70, 8, 8);
        gfx.DrawString(label, new XFont("Arial", 7, XFontStyleEx.Bold), new XSolidBrush(Lime), x + 12, y + 21);
        gfx.DrawString(value, new XFont("Arial", 10, XFontStyleEx.Bold), XBrushes.White, x + 12, y + 45);
    }

    private static void DrawLabelValue(XGraphics gfx, double x, double y, string label, string value)
    {
        gfx.DrawString(label, new XFont("Arial", 7, XFontStyleEx.Bold), XBrushes.Gray, x, y);
        gfx.DrawString(value, new XFont("Arial", 10, XFontStyleEx.Bold), new XSolidBrush(DarkGreen), x, y + 21);
    }

    private static string Number(decimal? value, string suffix = "") =>
        value.HasValue ? value.Value.ToString("0.00", CultureInfo.InvariantCulture) + suffix : "No calculable";

    private static void EnsureFonts()
    {
        PdfSharp.Fonts.GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }
}

internal static class PdfAttachmentMerger
{
    public static byte[] Merge(byte[] mainReport, IReadOnlyList<ReportAttachment> attachments)
    {
        using var result = new PdfDocument();
        using (var input = PdfReader.Open(new MemoryStream(mainReport), PdfDocumentOpenMode.Import))
            for (var index = 0; index < input.PageCount; index++) result.AddPage(input.Pages[index]);

        foreach (var attachment in attachments)
        {
            if (attachment.MimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                using var pdf = PdfReader.Open(new MemoryStream(attachment.Content), PdfDocumentOpenMode.Import);
                for (var index = 0; index < pdf.PageCount; index++) result.AddPage(pdf.Pages[index]);
                continue;
            }
            if (!attachment.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) continue;
            var page = result.AddPage();
            page.Size = PdfSharp.PageSize.Letter;
            using var gfx = XGraphics.FromPdfPage(page);
            using var imageStream = new MemoryStream(attachment.Content, writable: false);
            using var image = XImage.FromStream(imageStream);
            const double margin = 42;
            var scale = Math.Min((page.Width.Point - margin * 2) / image.PixelWidth, (page.Height.Point - 110) / image.PixelHeight);
            var width = image.PixelWidth * scale;
            var height = image.PixelHeight * scale;
            gfx.DrawString(attachment.Name, new XFont("Arial", 11, XFontStyleEx.Bold), XBrushes.Black, margin, 38);
            gfx.DrawImage(image, (page.Width.Point - width) / 2, 62, width, height);
        }
        using var output = new MemoryStream();
        result.Save(output, closeStream: false);
        return output.ToArray();
    }
}
