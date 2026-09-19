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
    private static readonly XColor Green900 = XColor.FromArgb(15, 68, 54);
    private static readonly XColor Green800 = XColor.FromArgb(23, 90, 71);
    private static readonly XColor Green600 = XColor.FromArgb(35, 128, 102);
    private static readonly XColor Leaf = XColor.FromArgb(136, 192, 97);
    private static readonly XColor LeafLight = XColor.FromArgb(234, 245, 228);
    private static readonly XColor Ink = XColor.FromArgb(19, 36, 32);
    private static readonly XColor Muted = XColor.FromArgb(92, 107, 102);
    private static readonly XColor Border = XColor.FromArgb(220, 230, 225);
    private static readonly XColor Surface = XColor.FromArgb(243, 247, 245);
    private static readonly XColor Amber = XColor.FromArgb(201, 138, 26);
    private static readonly XColor Red = XColor.FromArgb(179, 56, 44);
    private const double Left = 42;
    private const double Right = 570;

    public static byte[] Build(ReportGenerationData data, bool official)
    {
        EnsureFonts();
        using var document = new PdfDocument();
        document.Info.Title = $"Informe de Evaluación BPM - {data.EvaluationNumber}";
        document.Info.Author = "SIGERSA - DIGEMAPS";
        DrawCover(document.AddPage(), data, official);
        DrawOverview(document, data);
        DrawFindings(document, data);
        DrawFollowUps(document, data);
        DrawEvidenceIndex(document, data);
        DrawValidation(document, data, official);
        using var output = new MemoryStream();
        document.Save(output, closeStream: false);
        return output.ToArray();
    }

    private static void DrawCover(PdfPage page, ReportGenerationData data, bool official)
    {
        page.Size = PdfSharp.PageSize.Letter;
        using var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawRectangle(new XSolidBrush(Green900), 0, 0, page.Width.Point, page.Height.Point);
        gfx.DrawEllipse(new XSolidBrush(XColor.FromArgb(28, 111, 83)), -165, -185, 430, 330);
        gfx.DrawEllipse(new XSolidBrush(XColor.FromArgb(48, 132, 103)), 390, 595, 330, 330);
        gfx.DrawRectangle(new XSolidBrush(Leaf), 0, 0, page.Width.Point, 6);
        gfx.DrawString("SIGERSA", Font(30, true), XBrushes.White, Left, 70);
        gfx.DrawString(official ? "INFORME OFICIAL DE AUDITORÍA SANITARIA" : "BORRADOR PARA REVISIÓN",
            Font(8.5, true), new XSolidBrush(Leaf), Left, 170);
        if (data.Status.Equals("NO_APROBADA", StringComparison.OrdinalIgnoreCase))
        {
            gfx.DrawRoundedRectangle(new XPen(Red, 4), XBrushes.White, 313, 85, 257, 55, 7, 7);
            gfx.DrawString("EVALUACIÓN NO APROVADA", Font(15, true), new XSolidBrush(Red),
                new XRect(318, 101, 247, 27), XStringFormats.Center);
        }
        var formatter = new XTextFormatter(gfx);
        formatter.DrawString("Evaluación de Buenas\nPrácticas de Manufactura (BPM)", Font(34, true),
            XBrushes.White, new XRect(Left, 200, 520, 120));
        formatter.DrawString("Reporte técnico de cumplimiento normativo, gestión de riesgo sanitario y trazabilidad, elaborado conforme a los estándares de SIGERSA.",
            Font(11), new XSolidBrush(XColor.FromArgb(220, 239, 232)), new XRect(Left, 325, 490, 55));
        DrawDarkCard(gfx, Left, 398, 165, "N.º DE EVALUACIÓN", data.EvaluationNumber);
        DrawDarkCard(gfx, 220, 398, 165, "N.º DE CASO", data.CaseNumber);
        DrawDarkCard(gfx, 398, 398, 172, "CLASIFICACIÓN DE RIESGO", data.RiskLevel ?? "NO CALCULABLE");
        gfx.DrawRoundedRectangle(XBrushes.White, Left, 500, 528, 158, 11, 11);
        DrawCoverValue(gfx, 66, 532, "EMPRESA", data.CompanyName, 205);
        DrawCoverValue(gfx, 292, 532, "ESTABLECIMIENTO", data.EstablishmentName, 250);
        DrawCoverValue(gfx, 66, 593, "TÉCNICO EVALUADOR", data.EvaluatorName, 205);
        DrawCoverValue(gfx, 292, 593, "CUMPLIMIENTO BPM", Number(data.CompliancePercentage, "%"), 250);
        gfx.DrawString($"Generado el {DateTimeOffset.UtcNow:dd/MM/yyyy HH:mm} UTC", Font(7.5),
            new XSolidBrush(XColor.FromArgb(190, 220, 203)), Left, 749);
        gfx.DrawString("DIGEMAPS · Sistema Integral de Gestión de Riesgo y Seguridad Alimentaria",
            Font(7.5), new XSolidBrush(XColor.FromArgb(190, 220, 203)), Left, 767);
    }

    private static void DrawOverview(PdfDocument document, ReportGenerationData data)
    {
        var page = AddContentPage(document, data);
        using var gfx = XGraphics.FromPdfPage(page);
        var y = 92d;
        y = PageTitle(gfx, y, "1. Información General de la Evaluación",
            "Identificación del establecimiento y datos de trazabilidad del proceso");
        DrawInfoCell(gfx, Left, y, 264, "EMPRESA", data.CompanyName);
        DrawInfoCell(gfx, 306, y, 264, "ESTABLECIMIENTO", data.EstablishmentName);
        y += 51;
        DrawInfoCell(gfx, Left, y, 528, "DIRECCIÓN DEL ESTABLECIMIENTO", data.Address);
        y += 51;
        DrawInfoCell(gfx, Left, y, 264, "TÉCNICO EVALUADOR", data.EvaluatorName);
        DrawInfoCell(gfx, 306, y, 264, "FECHA Y HORA DE EJECUCIÓN", DateRange(data));
        y += 51;
        DrawInfoCell(gfx, Left, y, 264, "N.º DE EVALUACIÓN", data.EvaluationNumber);
        DrawInfoCell(gfx, 306, y, 264, "N.º DE CASO", data.CaseNumber);
        y += 76;
        y = SectionTitle(gfx, y, "2", "Resumen Ejecutivo");
        DrawKpi(gfx, Left, y, 126, "RIESGO DEL PRODUCTO", Number(data.ProductRisk), "Índice sanitario");
        DrawKpi(gfx, 177, y, 126, "RIESGO DEL ESTABLECIMIENTO", Number(data.EstablishmentRisk), "Índice sanitario");
        DrawKpi(gfx, 312, y, 126, "RIESGO TOTAL", Number(data.TotalRisk), data.RiskLevel ?? "No calculable");
        DrawKpi(gfx, 447, y, 123, "FRECUENCIA", data.Frequency ?? "No aplica", "Próxima inspección");
        y += 88;
        DrawComplianceGauge(gfx, y, data.CompliancePercentage);
        DrawRiskScale(gfx, y + 126, data.TotalRisk);
    }

    private static void DrawFindings(PdfDocument document, ReportGenerationData data)
    {
        var page = AddContentPage(document, data);
        var gfx = XGraphics.FromPdfPage(page);
        var y = SectionTitle(gfx, 96, "3", "Hallazgos y No Conformidades");
        if (data.Findings.Count == 0)
        {
            gfx.DrawRoundedRectangle(new XSolidBrush(LeafLight), Left, y, 528, 62, 8, 8);
            gfx.DrawRectangle(new XSolidBrush(Green600), Left, y, 5, 62);
            gfx.DrawEllipse(new XSolidBrush(Green600), 58, y + 13, 34, 34);
            gfx.DrawString("✓", Font(17, true), XBrushes.White, new XRect(58, y + 15, 34, 25), XStringFormats.Center);
            gfx.DrawString("No se registraron no conformidades", Font(10.5, true), new XSolidBrush(Green800), 108, y + 23);
            gfx.DrawString("La evaluación no identificó desviaciones respecto a los criterios BPM verificados.", Font(8.5), new XSolidBrush(Muted), 108, y + 43);
            y += 86;
        }
        else
        {
            DrawFindingHeader(gfx, y);
            y += 28;
            foreach (var finding in data.Findings)
            {
                var rowHeight = Math.Max(42, EstimateHeight(finding.Description, 270, 8) + 16);
                if (y + rowHeight > 695)
                {
                    gfx.Dispose();
                    page = AddContentPage(document, data);
                    gfx = XGraphics.FromPdfPage(page);
                    y = SectionTitle(gfx, 96, "3", "Hallazgos y No Conformidades (continuación)");
                    DrawFindingHeader(gfx, y);
                    y += 28;
                }
                gfx.DrawRectangle(new XSolidBrush(Surface), Left, y, 528, rowHeight);
                gfx.DrawString(finding.Code, Font(8, true), new XSolidBrush(Green800), 51, y + 18);
                gfx.DrawString(finding.Criticality, Font(7.5, true), new XSolidBrush(Red), 124, y + 18);
                DrawWrapped(gfx, finding.Description, Font(8), new XSolidBrush(Ink), 215, y + 10, 270, rowHeight - 12);
                gfx.DrawString(finding.Status, Font(7.5, true), new XSolidBrush(Muted), 497, y + 18);
                y += rowHeight + 2;
            }
        }
        gfx.Dispose();
    }

    private static void DrawFollowUps(PdfDocument document, ReportGenerationData data)
    {
        var entries = data.CorrectiveMeasures.Select(item => (Kind: "MEDIDA CORRECTIVA", Item: item))
            .Concat(data.Recommendations.Select(item => (Kind: "RECOMENDACIÓN", Item: item))).ToArray();
        var page = AddContentPage(document, data);
        var gfx = XGraphics.FromPdfPage(page);
        var y = SectionTitle(gfx, 96, "4", "Medidas Correctivas y Recomendaciones");
        if (entries.Length == 0)
        {
            gfx.DrawRoundedRectangle(new XSolidBrush(LeafLight), Left, y, 528, 58, 8, 8);
            gfx.DrawString("No se registraron medidas correctivas ni recomendaciones.", Font(9),
                new XSolidBrush(Green800), 60, y + 33);
            gfx.Dispose();
            return;
        }

        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var rowHeight = Math.Max(58, EstimateHeight(entry.Item.Detail, 390, 8.3) + 30);
            if (y + rowHeight > 700)
            {
                gfx.Dispose();
                page = AddContentPage(document, data);
                gfx = XGraphics.FromPdfPage(page);
                y = SectionTitle(gfx, 96, "4", "Medidas Correctivas y Recomendaciones (continuación)");
            }
            gfx.DrawRoundedRectangle(new XPen(Border), new XSolidBrush(Surface), Left, y, 528, rowHeight, 7, 7);
            gfx.DrawEllipse(new XSolidBrush(Green800), 54, y + 14, 26, 26);
            gfx.DrawString((index + 1).ToString("00", CultureInfo.InvariantCulture), Font(7.5, true), XBrushes.White,
                new XRect(54, y + 19, 26, 15), XStringFormats.Center);
            gfx.DrawString(entry.Kind, Font(7, true), new XSolidBrush(Green800), 94, y + 19);
            DrawWrapped(gfx, entry.Item.Detail, Font(8.3), new XSolidBrush(Ink), 94, y + 27, 390, rowHeight - 31);
            var dueDate = entry.Item.DueDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Sin fecha";
            gfx.DrawString(dueDate, Font(7.2, true), new XSolidBrush(Red),
                new XRect(484, y + 17, 75, 18), XStringFormats.Center);
            y += rowHeight + 8;
        }
        gfx.Dispose();
    }

    private static void DrawEvidenceIndex(PdfDocument document, ReportGenerationData data)
    {
        var page = AddContentPage(document, data);
        var gfx = XGraphics.FromPdfPage(page);
        var y = SectionTitle(gfx, 96, "5", "Anexo de Evidencias");
        DrawWrapped(gfx, "Registro documental asociado a la evaluación. Los archivos originales se incorporan a continuación de este índice.",
            Font(8.5), new XSolidBrush(Muted), Left, y, 528, 30);
        y += 39;
        if (data.Evidences.Count == 0)
        {
            gfx.DrawRoundedRectangle(new XSolidBrush(Surface), Left, y, 528, 55, 8, 8);
            gfx.DrawString("No se adjuntaron evidencias a esta evaluación.", Font(9), new XSolidBrush(Muted), 60, y + 31);
        }
        for (var index = 0; index < data.Evidences.Count; index++)
        {
            if (y + 68 > 700)
            {
                gfx.Dispose();
                page = AddContentPage(document, data);
                gfx = XGraphics.FromPdfPage(page);
                y = SectionTitle(gfx, 96, "5", "Anexo de Evidencias (continuación)");
            }
            var evidence = data.Evidences[index];
            gfx.DrawRoundedRectangle(new XPen(Border), XBrushes.White, Left, y, 528, 56, 7, 7);
            gfx.DrawRoundedRectangle(new XSolidBrush(Green800), 54, y + 12, 35, 32, 5, 5);
            gfx.DrawString((index + 1).ToString("00", CultureInfo.InvariantCulture), Font(10, true), XBrushes.White,
                new XRect(54, y + 20, 35, 16), XStringFormats.Center);
            gfx.DrawString(evidence.Name, Font(9, true), new XSolidBrush(Ink), 104, y + 22);
            gfx.DrawString($"{evidence.Type} · {evidence.MimeType}", Font(7.5), new XSolidBrush(Muted), 104, y + 40);
            y += 65;
        }
        gfx.Dispose();
    }

    private static void DrawValidation(PdfDocument document, ReportGenerationData data, bool official)
    {
        var page = AddContentPage(document, data);
        using var gfx = XGraphics.FromPdfPage(page);
        var y = SectionTitle(gfx, 96, "6", "Validación y Trazabilidad del Informe");
        DrawSignature(gfx, Left, y + 8, 254, data.EvaluatorName, "Técnico Evaluador · SIGERSA");
        DrawSignature(gfx, 316, y + 8, 254, official ? "Supervisión Técnica" : "Pendiente de validación", "DIGEMAPS · Validación de Caso");
        y += 137;
        gfx.DrawString("TRAZABILIDAD DOCUMENTAL", Font(8, true), new XSolidBrush(Green800), Left, y);
        y += 17;
        DrawInfoCell(gfx, Left, y, 264, "EVALUACIÓN", data.EvaluationNumber);
        DrawInfoCell(gfx, 306, y, 264, "CASO", data.CaseNumber);
        y += 51;
        DrawInfoCell(gfx, Left, y, 264, "ESTADO", data.Status);
        DrawInfoCell(gfx, 306, y, 264, "TIPO DE DOCUMENTO", official ? "Informe oficial" : "Borrador para revisión");
        y += 80;
        gfx.DrawString("Aviso de confidencialidad y control documental", Font(9, true), new XSolidBrush(Ink), Left, y);
        DrawWrapped(gfx,
            $"Este informe fue generado automáticamente por SIGERSA el {DateTimeOffset.UtcNow:dd/MM/yyyy 'a las' HH:mm} UTC. Su contenido refleja el estado de cumplimiento del establecimiento en la fecha de ejecución y se conserva como documento institucional trazable. La reproducción o divulgación debe realizarse conforme a las políticas vigentes de DIGEMAPS.",
            Font(8), new XSolidBrush(Muted), Left, y + 18, 528, 85);
    }

    private static PdfPage AddContentPage(PdfDocument document, ReportGenerationData data)
    {
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.Letter;
        using var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawString("SIGERSA", Font(13, true), new XSolidBrush(Green800), Left, 35);
        gfx.DrawString("INFORME DE EVALUACIÓN BPM", Font(8.5, true), new XSolidBrush(Green800), 116, 30);
        gfx.DrawString($"{data.EvaluationNumber} · {data.CaseNumber}", Font(7), new XSolidBrush(Muted), 116, 43);
        gfx.DrawLine(new XPen(Green600, 1.5), Left, 58, Right, 58);
        gfx.DrawLine(new XPen(Border, 1), Left, 747, Right, 747);
        gfx.DrawString("SIGERSA © 2026 · Documento institucional", Font(7), new XSolidBrush(Muted), Left, 765);
        gfx.DrawString($"Página {document.PageCount}", Font(7), new XSolidBrush(Muted), 522, 765);
        return page;
    }

    private static double PageTitle(XGraphics gfx, double y, string title, string kicker)
    {
        gfx.DrawString(title, Font(16, true), new XSolidBrush(Green900), Left, y);
        gfx.DrawString(kicker.ToUpperInvariant(), Font(7, true), new XSolidBrush(Green600), Left, y + 22);
        gfx.DrawLine(new XPen(Border, 1.5), Left, y + 35, Right, y + 35);
        return y + 49;
    }

    private static double SectionTitle(XGraphics gfx, double y, string number, string title)
    {
        gfx.DrawRoundedRectangle(new XSolidBrush(Green800), Left, y - 15, 27, 27, 5, 5);
        gfx.DrawString(number, Font(10, true), XBrushes.White, new XRect(Left, y - 10, 27, 18), XStringFormats.Center);
        gfx.DrawString(title, Font(13, true), new XSolidBrush(Green900), 80, y + 4);
        gfx.DrawLine(new XPen(Border, 1), 80 + Math.Min(300, title.Length * 7), y, Right, y);
        return y + 25;
    }

    private static void DrawInfoCell(XGraphics gfx, double x, double y, double width, string label, string value)
    {
        gfx.DrawRectangle(new XPen(Border), new XSolidBrush(Surface), x, y, width, 48);
        gfx.DrawString(label, Font(6.8, true), new XSolidBrush(Muted), x + 12, y + 16);
        DrawWrapped(gfx, value, Font(8.8, true), new XSolidBrush(Ink), x + 12, y + 24, width - 24, 20);
    }

    private static void DrawKpi(XGraphics gfx, double x, double y, double width, string label, string value, string note)
    {
        gfx.DrawRoundedRectangle(new XPen(Border), new XSolidBrush(Surface), x, y, width, 72, 7, 7);
        gfx.DrawRectangle(new XSolidBrush(Green600), x, y, width, 3);
        gfx.DrawString(label, Font(6.2, true), new XSolidBrush(Muted), x + 9, y + 17);
        gfx.DrawString(value, Font(value.Length > 10 ? 11 : 16, true), new XSolidBrush(Green800), x + 9, y + 43);
        gfx.DrawString(note, Font(6.3), new XSolidBrush(Muted), x + 9, y + 61);
    }

    private static void DrawComplianceGauge(XGraphics gfx, double y, decimal? compliance)
    {
        gfx.DrawRoundedRectangle(new XPen(Border), XBrushes.White, Left, y, 528, 108, 8, 8);
        const double cx = 98;
        var cy = y + 54;
        gfx.DrawEllipse(new XPen(Border, 10), cx - 39, cy - 39, 78, 78);
        var value = Math.Clamp((double)(compliance ?? 0), 0, 100);
        if (value > 0) gfx.DrawArc(new XPen(Green600, 10), cx - 39, cy - 39, 78, 78, -90, value * 3.6);
        gfx.DrawEllipse(XBrushes.White, cx - 29, cy - 29, 58, 58);
        gfx.DrawString(compliance.HasValue ? $"{compliance:0.#}%" : "N/D", Font(14, true), new XSolidBrush(Green800),
            new XRect(cx - 30, cy - 9, 60, 18), XStringFormats.Center);
        gfx.DrawString("Cumplimiento de Buenas Prácticas de Manufactura", Font(10.2, true), new XSolidBrush(Ink), 157, y + 30);
        DrawWrapped(gfx, $"El establecimiento alcanzó {Number(compliance, "%")} de cumplimiento sobre los criterios BPM aplicables.",
            Font(8.2), new XSolidBrush(Muted), 157, y + 42, 376, 30);
        gfx.DrawRoundedRectangle(new XSolidBrush(Border), 157, y + 80, 365, 7, 3, 3);
        gfx.DrawRoundedRectangle(new XSolidBrush(Green600), 157, y + 80, 365 * value / 100d, 7, 3, 3);
    }

    private static void DrawRiskScale(XGraphics gfx, double y, decimal? risk)
    {
        gfx.DrawRoundedRectangle(new XPen(Border), XBrushes.White, Left, y, 528, 78, 8, 8);
        gfx.DrawString("Escala de Clasificación de Riesgo Total", Font(9, true), new XSolidBrush(Ink), 58, y + 20);
        const double trackX = 62;
        const double trackWidth = 488;
        gfx.DrawRectangle(new XSolidBrush(Leaf), trackX, y + 35, trackWidth * .325, 13);
        gfx.DrawRectangle(new XSolidBrush(Amber), trackX + trackWidth * .325, y + 35, trackWidth * .3375, 13);
        gfx.DrawRectangle(new XSolidBrush(Red), trackX + trackWidth * .6625, y + 35, trackWidth * .3375, 13);
        var normalized = Math.Clamp(((double)(risk ?? 1) - 1d) / 8d, 0, 1);
        var markerX = trackX + trackWidth * normalized;
        gfx.DrawLine(new XPen(Ink, 2), markerX, y + 29, markerX, y + 54);
        gfx.DrawString(Number(risk), Font(7, true), new XSolidBrush(Ink), markerX - 10, y + 27);
        gfx.DrawString("Bajo (1.0-3.6)", Font(6.5), new XSolidBrush(Muted), trackX, y + 65);
        gfx.DrawString("Medio (>3.6-6.3)", Font(6.5), new XSolidBrush(Muted), 260, y + 65);
        gfx.DrawString("Alto (>6.3-9.0)", Font(6.5), new XSolidBrush(Muted), 456, y + 65);
    }

    private static void DrawFindingHeader(XGraphics gfx, double y)
    {
        gfx.DrawRectangle(new XSolidBrush(Green800), Left, y, 528, 26);
        gfx.DrawString("CÓDIGO", Font(7, true), XBrushes.White, 51, y + 17);
        gfx.DrawString("CRITICIDAD", Font(7, true), XBrushes.White, 124, y + 17);
        gfx.DrawString("DESCRIPCIÓN", Font(7, true), XBrushes.White, 215, y + 17);
        gfx.DrawString("ESTADO", Font(7, true), XBrushes.White, 497, y + 17);
    }

    private static void DrawSignature(XGraphics gfx, double x, double y, double width, string name, string role)
    {
        gfx.DrawRoundedRectangle(new XPen(Border), XBrushes.White, x, y, width, 104, 8, 8);
        gfx.DrawLine(new XPen(Muted, 1), x + 28, y + 58, x + width - 28, y + 58);
        gfx.DrawString(name, Font(9, true), new XSolidBrush(Ink), new XRect(x + 8, y + 69, width - 16, 15), XStringFormats.Center);
        gfx.DrawString(role, Font(7), new XSolidBrush(Muted), new XRect(x + 8, y + 86, width - 16, 12), XStringFormats.Center);
    }

    private static void DrawDarkCard(XGraphics gfx, double x, double y, double width, string label, string value)
    {
        gfx.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(45, 122, 94)), x, y, width, 68, 8, 8);
        gfx.DrawString(label, Font(6.5, true), new XSolidBrush(Leaf), x + 11, y + 20);
        DrawWrapped(gfx, value, Font(9.2, true), XBrushes.White, x + 11, y + 30, width - 22, 28);
    }

    private static void DrawCoverValue(XGraphics gfx, double x, double y, string label, string value, double width)
    {
        gfx.DrawString(label, Font(6.5, true), new XSolidBrush(Muted), x, y);
        DrawWrapped(gfx, value, Font(10, true), new XSolidBrush(Green800), x, y + 10, width, 35);
    }

    private static void DrawWrapped(XGraphics gfx, string text, XFont font, XBrush brush,
        double x, double y, double width, double height) =>
        new XTextFormatter(gfx).DrawString(text, font, brush, new XRect(x, y, width, height));

    private static double EstimateHeight(string text, double width, double size) =>
        Math.Ceiling(Math.Max(1, text.Length / Math.Max(20, width / (size * .55)))) * (size + 4);

    private static string DateRange(ReportGenerationData data)
    {
        if (!data.StartedAt.HasValue && !data.FinishedAt.HasValue) return "No registrada";
        var start = data.StartedAt?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? "No registrada";
        var end = data.FinishedAt?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        return end is null ? start : $"{start} - {end}";
    }

    private static string Number(decimal? value, string suffix = "") =>
        value.HasValue ? value.Value.ToString("0.00", CultureInfo.InvariantCulture) + suffix : "No calculable";

    private static XFont Font(double size, bool bold = false) =>
        new("Arial", size, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);

    private static void EnsureFonts() => PdfSharp.Fonts.GlobalFontSettings.UseWindowsFontsUnderWindows = true;
}

internal static class PdfAttachmentMerger
{
    public static byte[] Merge(byte[] mainReport, IReadOnlyList<ReportAttachment> attachments)
    {
        PdfSharp.Fonts.GlobalFontSettings.UseWindowsFontsUnderWindows = true;
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
