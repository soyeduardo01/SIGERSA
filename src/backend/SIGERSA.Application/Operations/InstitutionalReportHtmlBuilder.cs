using System.Globalization;
using System.Net;
using System.Text;
using QRCoder;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Application.Operations;

public static class InstitutionalReportHtmlBuilder
{
    private const string Styles = """
        @page { size: Letter; margin: 0; }
        * { box-sizing: border-box; }
        html,body { margin:0; background:#eef3f0; color:#132420; font-family:Poppins,Arial,sans-serif; -webkit-print-color-adjust:exact; print-color-adjust:exact; }
        .page { position:relative; width:8.5in; height:11in; margin:0 auto; padding:.58in .58in .78in; background:#fff; break-after:page; page-break-after:always; overflow:hidden; }
        .page:last-child { break-after:auto; page-break-after:auto; }
        .cover { background:#0f4436; color:#fff; padding:.52in .58in; }
        .cover::before,.cover::after { content:""; position:absolute; border-radius:50%; background:#1c6f53; opacity:.55; }
        .cover::before { width:4.2in; height:3.2in; left:-1.6in; top:-1.7in; }
        .cover::after { width:3.4in; height:3.4in; right:-1.4in; bottom:-1.1in; }
        .logos,.brand { position:relative; z-index:1; display:flex; align-items:center; justify-content:flex-start; gap:16px; }
        .sigersa { width:1.25in; height:.32in; object-fit:contain; }
        .cover .sigersa { width:2.35in; height:.55in; filter:brightness(0) invert(1); }
        .cover-main { position:relative; z-index:1; margin-top:.68in; }
        .rejected-stamp { position:absolute; z-index:3; right:.56in; top:.68in; width:3.25in; padding:12px 14px; border:5px double #d52b1e; border-radius:8px; color:#d52b1e; background:rgba(255,255,255,.94); font-size:20px; font-weight:900; letter-spacing:1px; text-align:center; transform:rotate(-7deg); box-shadow:0 2px 0 rgba(0,0,0,.12); }
        .eyebrow,.label,.kicker { font-size:8px; font-weight:700; letter-spacing:1px; text-transform:uppercase; }
        .eyebrow { color:#a6d887; }
        h1 { max-width:7in; font-size:43px; line-height:1.07; letter-spacing:-1.2px; margin:.18in 0 .2in; color:#fff; }
        .lead { max-width:6.4in; color:#dcefe8; font-size:11px; line-height:1.7; }
        .chips,.kpis { display:grid; grid-template-columns:repeat(3,1fr); gap:11px; margin-top:.34in; }
        .chip { padding:14px; background:#246f59; border-radius:9px; }
        .chip small { display:block; color:#a6d887; font-size:7px; text-transform:uppercase; }
        .chip b { display:block; margin-top:7px; font-size:11px; }
        .cover-card,.grid { display:grid; grid-template-columns:1fr 1fr; gap:10px; }
        .cover-card { margin-top:.35in; padding:20px; border-radius:13px; background:#fff; color:#132420; gap:18px 28px; }
        .cell { min-height:57px; padding:12px; background:#f3f7f5; border:1px solid #dce6e1; }
        .cover-card .cell { padding:0; min-height:42px; background:#fff; border:0; }
        .label { display:block; color:#5c6b66; font-size:7px; }
        .value { display:block; margin-top:6px; color:#175a47; font-size:10px; font-weight:700; }
        .cover-foot,footer { position:absolute; left:.58in; right:.58in; bottom:.32in; display:flex; justify-content:space-between; font-size:7px; }
        .cover-foot { z-index:1; color:#c8e2d8; }
        header { display:flex; align-items:center; justify-content:space-between; border-bottom:2px solid #238066; padding-bottom:10px; break-inside:avoid; page-break-inside:avoid; }
        header span { font-size:8px; color:#5c6b66; text-align:right; }
        h2 { font-size:20px; color:#0f4436; margin:.34in 0 4px; }
        .kicker { color:#238066; }
        .section { margin-top:.28in; }
        .section-title { display:flex; align-items:center; gap:10px; margin:18px 0 13px; color:#0f4436; font-size:15px; font-weight:700; }
        .section-title i { font-style:normal; display:grid; place-items:center; width:28px; height:28px; border-radius:6px; background:#175a47; color:#fff; font-size:10px; }
        .kpis { grid-template-columns:repeat(4,1fr); margin-top:0; }
        .kpi { border-top:4px solid #238066; border-radius:7px; background:#f3f7f5; padding:11px; }
        .kpi span { font-size:7px; color:#5c6b66; text-transform:uppercase; font-weight:700; }
        .kpi b { display:block; color:#175a47; font-size:15px; margin-top:8px; }
        .visual-grid { display:grid; grid-template-columns:.82fr 1.38fr; gap:12px; margin-top:16px; }
        .visual-card { min-height:142px; padding:16px 18px; border:1px solid #dce6e1; border-radius:11px; background:#fff; break-inside:avoid; }
        .visual-card h3 { margin:0; color:#132420; font-size:10px; }
        .visual-card p { margin:8px 0 0; color:#5c6b66; font-size:7.5px; line-height:1.55; }
        .compliance-visual { display:flex; align-items:center; gap:16px; height:100%; }
        .ring { --pct:0; position:relative; flex:0 0 83px; width:83px; height:83px; display:grid; place-items:center; border-radius:50%; background:conic-gradient(#238066 calc(var(--pct) * 1%),#dce6e1 0); }
        .ring::after { content:""; position:absolute; inset:10px; border-radius:50%; background:#fff; }
        .ring b { position:relative; z-index:1; color:#175a47; font-size:15px; }
        .mini-bar { height:7px; margin-top:12px; overflow:hidden; border-radius:10px; background:#dce6e1; }
        .mini-bar i { display:block; height:100%; border-radius:10px; background:#238066; }
        .risk-scale { position:relative; margin-top:19px; }
        .risk-value { position:absolute; top:-18px; transform:translateX(-50%); color:#132420; font-size:8px; font-weight:700; }
        .risk-marker { position:absolute; top:-8px; width:2px; height:32px; background:#132420; }
        .risk-track { display:grid; grid-template-columns:32.5fr 33.75fr 33.75fr; height:15px; overflow:hidden; border-radius:5px; }
        .risk-low { background:#87bf65; } .risk-mid { background:#ca8c19; } .risk-high { background:#b3382c; }
        .risk-labels { display:grid; grid-template-columns:32.5fr 33.75fr 33.75fr; margin-top:8px; color:#5c6b66; font-size:6.2px; }
        .risk-labels span:nth-child(2) { text-align:center; } .risk-labels span:last-child { text-align:right; }
        .decision-flow { display:grid; grid-template-columns:1fr 20px 1fr 20px 1fr 20px 1fr; align-items:stretch; margin-top:12px; }
        .flow-node { min-height:57px; padding:9px 10px; border:1px solid #dce6e1; border-radius:8px; background:#f3f7f5; }
        .flow-node small { display:block; color:#5c6b66; font-size:6px; font-weight:700; letter-spacing:.55px; text-transform:uppercase; }
        .flow-node b { display:block; margin-top:6px; color:#175a47; font-size:10px; }
        .flow-arrow { display:grid; place-items:center; color:#238066; font-size:15px; font-weight:700; }
        .summary-strip { display:grid; grid-template-columns:repeat(3,1fr); gap:10px; margin:14px 0; }
        .summary-pill { display:flex; align-items:center; gap:10px; padding:10px 12px; border:1px solid #dce6e1; border-radius:9px; background:#fff; }
        .summary-pill b { display:grid; place-items:center; width:28px; height:28px; border-radius:7px; background:#eaf5e4; color:#175a47; font-size:12px; }
        .summary-pill span { color:#5c6b66; font-size:7px; font-weight:700; text-transform:uppercase; }
        .process-flow { display:grid; grid-template-columns:1fr 28px 1fr 28px 1fr; align-items:center; margin:14px 0 17px; }
        .process-step { padding:12px; border-radius:9px; background:#f3f7f5; border:1px solid #dce6e1; }
        .process-step small { color:#238066; font-size:6px; font-weight:700; letter-spacing:.7px; text-transform:uppercase; }
        .process-step b { display:block; margin-top:5px; color:#132420; font-size:8px; }
        table { width:100%; table-layout:fixed; border-collapse:collapse; font-size:8px; }
        thead { display:table-header-group; }
        tr { break-inside:avoid; page-break-inside:avoid; }
        th { padding:9px; background:#175a47; color:#fff; text-align:left; text-transform:uppercase; }
        td { padding:10px; border-bottom:2px solid #fff; background:#f3f7f5; vertical-align:top; overflow-wrap:anywhere; }
        th:nth-child(1),td:nth-child(1) { width:15%; } th:nth-child(2),td:nth-child(2) { width:16%; } th:nth-child(4),td:nth-child(4) { width:15%; }
        .severity { font-weight:700; color:#b3382c; }
        .empty { padding:20px; background:#eaf5e4; border-left:5px solid #238066; border-radius:8px; font-size:9px; }
        ol.recs { list-style:none; padding:0; counter-reset:item; }
        ol.recs li { counter-increment:item; display:flex; gap:12px; margin:11px 0; font-size:9px; line-height:1.55; }
        ol.recs li::before { content:counter(item); flex:0 0 24px; height:24px; display:grid; place-items:center; border-radius:50%; background:#175a47; color:#fff; }
        .followups { list-style:none; margin:0; padding:0; }
        .followups li { display:grid; grid-template-columns:30px 1fr auto; gap:10px; align-items:start; margin:9px 0; padding:11px; border:1px solid #dce6e1; border-radius:8px; background:#f3f7f5; font-size:8px; line-height:1.5; }
        .followups li>b { display:grid; place-items:center; width:25px; height:25px; border-radius:50%; background:#175a47; color:#fff; }
        .followups time { white-space:nowrap; color:#b3382c; font-size:7px; font-weight:700; }
        .evidence { list-style:none; padding:0; }
        .evidence li { display:flex; align-items:center; gap:14px; border:1px solid #dce6e1; border-radius:8px; padding:12px; margin:9px 0; }
        .evidence li>b { display:grid; place-items:center; width:38px; height:34px; background:#175a47; color:#fff; border-radius:6px; }
        .evidence strong,.evidence small { display:block; } .evidence small { margin-top:4px; color:#5c6b66; }
        .evidence-identity { display:flex; align-items:center; gap:16px; margin:14px 0; padding:10px 12px; border-left:3px solid #238066; background:#f3f7f5; }
        .signatures { display:grid; grid-template-columns:1fr 1fr; gap:18px; margin-top:22px; }
        .signature { height:112px; border:1px solid #dce6e1; border-radius:9px; text-align:center; padding-top:62px; }
        .signature hr { width:72%; border:0; border-top:1px solid #5c6b66; }
        .signature b,.signature span { display:block; font-size:9px; } .signature span { color:#5c6b66; font-size:7px; }
        .verification-card { display:grid; grid-template-columns:126px 1fr; gap:18px; align-items:center; margin-top:14px; padding:12px 16px; border:2px solid #238066; border-radius:11px; background:#f3f7f5; }
        .verification-card img { width:118px; height:118px; padding:4px; background:#fff; }
        .verification-card h3 { margin:0 0 7px; color:#0f4436; font-size:13px; }
        .verification-card p { margin:0 0 8px; color:#5c6b66; font-size:8px; line-height:1.5; }
        .verification-card code { display:block; color:#175a47; font-family:Arial,sans-serif; font-size:6.5px; overflow-wrap:anywhere; }
        footer { border-top:1px solid #dce6e1; padding-top:8px; color:#5c6b66; }
        """;

    public static string Build(
        ReportGenerationData data,
        bool official,
        InstitutionalReportAssets assets,
        string verificationUrl)
    {
        var openFindings = data.Findings.Count(item => !item.Status.Contains("CERR", StringComparison.OrdinalIgnoreCase));
        var priorityFindings = data.Findings.Count(item =>
            item.Criticality.Contains("ALT", StringComparison.OrdinalIgnoreCase)
            || item.Criticality.Contains("CRIT", StringComparison.OrdinalIgnoreCase));
        var findingsPageCount = Math.Max(1, (int)Math.Ceiling(data.Findings.Count / 8m));
        var findingsPages = FindingsPages(data, assets, openFindings, priorityFindings, 3);
        var followUpStartPage = 3 + findingsPageCount;
        var followUpPages = FollowUpPages(data, assets, followUpStartPage);
        var followUpPageCount = Math.Max(1, (int)Math.Ceiling(Math.Max(1, data.CorrectiveMeasures.Count + data.Recommendations.Count) / 8m));
        var evidenceStartPage = followUpStartPage + followUpPageCount;
        var evidenceChunks = SplitEvidence(data.Evidences);
        var evidencePages = EvidencePages(data, assets, evidenceStartPage, evidenceChunks);
        var validationPage = evidenceStartPage + evidenceChunks.Count;
        var qrCodeDataUri = QrCodeDataUri(verificationUrl);

        return $$"""
            <!doctype html><html lang="es"><head><meta charset="utf-8"><title>Informe {{E(data.EvaluationNumber)}}</title><style>
            @font-face { font-family:Poppins; src:url('{{assets.PoppinsRegularDataUri}}') format('truetype'); font-weight:400; }
            @font-face { font-family:Poppins; src:url('{{assets.PoppinsSemiBoldDataUri}}') format('truetype'); font-weight:600 900; }
            {{Styles}}</style></head><body>
            <section class="page cover"><div class="logos">{{Logos(assets)}}</div>{{RejectedStamp(data)}}<div class="cover-main"><div class="eyebrow">{{(official ? "Informe oficial de auditoría sanitaria" : "Borrador para revisión")}}</div><h1>Evaluación de Buenas<br>Prácticas de Manufactura</h1><p class="lead">Reporte técnico de cumplimiento normativo, gestión de riesgo sanitario y trazabilidad institucional.</p><div class="chips"><div class="chip"><small>N.º de evaluación</small><b>{{E(data.EvaluationNumber)}}</b></div><div class="chip"><small>N.º de caso</small><b>{{E(data.CaseNumber)}}</b></div><div class="chip"><small>Clasificación de riesgo</small><b>{{E(data.RiskLevel ?? "No calculable")}}</b></div></div><div class="cover-card">{{Cell("Empresa", data.CompanyName)}}{{Cell("Establecimiento", data.EstablishmentName)}}{{Cell("Técnico evaluador", data.EvaluatorName)}}{{Cell("Cumplimiento BPM", Number(data.CompliancePercentage, "%"))}}</div></div><div class="cover-foot"><span>Sistema Integral de Gestión de Riesgos Sanitarios</span><span>{{DateTimeOffset.UtcNow:dd/MM/yyyy}}</span></div></section>
            {{PageStart(assets, data, "Resumen ejecutivo", "Resultados generales de la evaluación")}}
            {{Title("1", "Identificación de la evaluación")}}<div class="grid">{{Cell("Empresa", data.CompanyName)}}{{Cell("Establecimiento", data.EstablishmentName)}}{{Cell("Dirección", data.Address)}}{{Cell("Técnico evaluador", data.EvaluatorName)}}{{Cell("Periodo de ejecución", DateRange(data))}}{{Cell("Estado", data.Status)}}</div>
            <div class="section">{{Title("2", "Indicadores de cumplimiento y riesgo")}}<div class="kpis">{{Kpi("Cumplimiento", Number(data.CompliancePercentage, "%"))}}{{Kpi("Riesgo producto", Number(data.ProductRisk))}}{{Kpi("Riesgo total", Number(data.TotalRisk))}}{{Kpi("Frecuencia", data.Frequency ?? "No aplica")}}</div><div class="visual-grid"><div class="visual-card"><div class="compliance-visual"><div class="ring" style="--pct:{{Percent(data.CompliancePercentage)}}"><b>{{CompactNumber(data.CompliancePercentage, "%")}}</b></div><div><h3>Cumplimiento BPM</h3><p>Porcentaje alcanzado sobre los criterios aplicables.</p><div class="mini-bar"><i style="width:{{Percent(data.CompliancePercentage)}}%"></i></div></div></div></div><div class="visual-card"><h3>Escala de clasificación de riesgo total</h3><div class="risk-scale"><span class="risk-value" style="left:{{RiskPercent(data.TotalRisk)}}%">{{Number(data.TotalRisk)}}</span><i class="risk-marker" style="left:{{RiskPercent(data.TotalRisk)}}%"></i><div class="risk-track"><span class="risk-low"></span><span class="risk-mid"></span><span class="risk-high"></span></div><div class="risk-labels"><span>Bajo (1.0-3.6)</span><span>Medio (&gt;3.6-6.3)</span><span>Alto (&gt;6.3-9.0)</span></div><p>El marcador ubica el resultado calculado dentro de la escala sanitaria.</p></div></div></div><h3 style="margin:14px 0 0;font-size:10px">Flujo de decisión sanitaria</h3><div class="decision-flow">{{FlowNode("Riesgo del producto", Number(data.ProductRisk))}}<span class="flow-arrow">×</span>{{FlowNode("Riesgo del establecimiento", Number(data.EstablishmentRisk))}}<span class="flow-arrow">→</span>{{FlowNode("Riesgo total", Number(data.TotalRisk))}}<span class="flow-arrow">→</span>{{FlowNode(data.RiskLevel ?? "Clasificación", data.Frequency ?? "No aplica")}}</div></div>{{Footer(data, 2)}}</section>
            {{findingsPages}}
            {{followUpPages}}
            {{evidencePages}}
            {{PageStart(assets, data, "Validación y trazabilidad", "Control institucional del documento")}}<div class="signatures">{{Signature(data.EvaluatorName, "Técnico evaluador · SIGERSA")}}{{Signature(official ? "Supervisión técnica" : "Pendiente de validación", "DIGEMAPS · Validación de caso")}}</div><div class="process-flow">{{ProcessStep("01", "Generación", "Informe consolidado")}}<span class="flow-arrow">→</span>{{ProcessStep("02", "Revisión", "Control técnico")}}<span class="flow-arrow">→</span>{{ProcessStep("03", official ? "Emisión" : "Pendiente", official ? "Documento oficial" : "Borrador")}}</div><div class="section"><div class="grid">{{Cell("Evaluación", data.EvaluationNumber)}}{{Cell("Caso", data.CaseNumber)}}{{Cell("Estado", data.Status)}}{{Cell("Tipo de documento", official ? "Informe oficial" : "Borrador para revisión")}}</div></div><div class="verification-card"><img src="{{qrCodeDataUri}}" alt="Código QR de verificación"><div><h3>Verifique la autenticidad de este informe</h3><p>Escanee el código QR para consultar el registro institucional, su versión y el estado de validez del documento.</p><code>{{E(verificationUrl)}}</code></div></div><div class="section" style="margin-top:12px"><b>Aviso de confidencialidad y control documental</b><p style="font-size:7.5px;line-height:1.5;color:#5c6b66">Generado automáticamente por SIGERSA el {{DateTimeOffset.UtcNow:dd/MM/yyyy 'a las' HH:mm}} UTC. Se conserva como documento institucional trazable.</p></div>{{Footer(data, validationPage)}}</section>
            </body></html>
            """;
    }

    private static string Logos(InstitutionalReportAssets a) => $"<img class=\"sigersa\" src=\"{a.SigersaLogoDataUri}\" alt=\"SIGERSA\">";
    private static string RejectedStamp(ReportGenerationData data) =>
        data.Status.Equals("NO_APROBADA", StringComparison.OrdinalIgnoreCase)
            ? "<div class=\"rejected-stamp\">EVALUACIÓN NO APROVADA</div>"
            : string.Empty;
    private static string PageStart(InstitutionalReportAssets a, ReportGenerationData d, string title, string kicker) => $"<section class=\"page\"><header><div class=\"brand\">{Logos(a)}</div><span>{E(d.EvaluationNumber)}<br>{E(d.CaseNumber)}</span></header><h2>{E(title)}</h2><div class=\"kicker\">{E(kicker)}</div>";
    private static string Footer(ReportGenerationData d, int page) => $"<footer><span>Documento institucional · SIGERSA / DIGEMAPS</span><span>{E(d.EvaluationNumber)} · Página {page}</span></footer>";
    private static string Title(string n, string t) => $"<div class=\"section-title\"><i>{E(n)}</i>{E(t)}</div>";
    private static string Cell(string l, string v) => $"<div class=\"cell\"><span class=\"label\">{E(l)}</span><span class=\"value\">{E(v)}</span></div>";
    private static string Kpi(string l, string v) => $"<div class=\"kpi\"><span>{E(l)}</span><b>{E(v)}</b></div>";
    private static string FlowNode(string l, string v) => $"<div class=\"flow-node\"><small>{E(l)}</small><b>{E(v)}</b></div>";
    private static string SummaryPill(int value, string label) => $"<div class=\"summary-pill\"><b>{value}</b><span>{E(label)}</span></div>";
    private static string ProcessStep(string number, string title, string note) => $"<div class=\"process-step\"><small>{E(number)} · {E(title)}</small><b>{E(note)}</b></div>";
    private static string Signature(string n, string r) => $"<div class=\"signature\"><hr><b>{E(n)}</b><span>{E(r)}</span></div>";
    private static string QrCodeDataUri(string value)
    {
        using var qrData = QRCodeGenerator.GenerateQrCode(value, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new SvgQRCode(qrData);
        var svg = qrCode.GetGraphic(4, "#0f4436", "#ffffff", drawQuietZones: true);
        return $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}";
    }
    private static string FindingRow(ReportFinding f) => $"<tr><td>{E(f.Code)}</td><td><span class=\"severity\">{E(f.Criticality)}</span></td><td>{E(f.Description)}</td><td>{E(f.Status)}</td></tr>";
    private static string EvidenceRow(ReportEvidence e, int i) => $"<li><b>{i + 1:00}</b><span><strong>{E(e.Name)}</strong><small>{E(e.Type)} · {E(e.MimeType)}</small></span></li>";
    private static string FindingsPages(ReportGenerationData data, InstitutionalReportAssets assets,
        int openFindings, int priorityFindings, int startPage)
    {
        var chunks = data.Findings.Chunk(8).ToArray();
        if (chunks.Length == 0) chunks = [[]];
        return string.Concat(chunks.Select((chunk, index) =>
        {
            var rows = chunk.Length == 0
                ? "<div class=\"empty\"><strong>No se registraron no conformidades.</strong></div>"
                : $"<table><thead><tr><th>Código</th><th>Criticidad</th><th>Descripción</th><th>Estado</th></tr></thead><tbody>{string.Join(string.Empty, chunk.Select(FindingRow))}</tbody></table>";
            var summary = index == 0
                ? $"<div class=\"summary-strip\">{SummaryPill(data.Findings.Count, "Hallazgos totales")}{SummaryPill(openFindings, "Pendientes de cierre")}{SummaryPill(priorityFindings, "Prioridad alta")}</div>"
                : string.Empty;
            var heading = index == 0 ? "Hallazgos y no conformidades" : "Hallazgos y no conformidades (continuación)";
            return $"{PageStart(assets, data, heading, "Detalle técnico de la inspección")}{Title("3", "Hallazgos y no conformidades")}{summary}{rows}{Footer(data, startPage + index)}</section>";
        }));
    }

    private static string FollowUpPages(ReportGenerationData data, InstitutionalReportAssets assets, int startPage)
    {
        var items = data.CorrectiveMeasures.Select(item => (Kind: "Medida correctiva", Item: item))
            .Concat(data.Recommendations.Select(item => (Kind: "Recomendación", Item: item))).ToArray();
        var chunks = items.Chunk(8).ToArray();
        if (chunks.Length == 0) chunks = [[]];
        var offset = 0;
        return string.Concat(chunks.Select((chunk, index) =>
        {
            var body = chunk.Length == 0
                ? "<div class=\"empty\"><strong>No se registraron medidas correctivas ni recomendaciones.</strong></div>"
                : $"<ul class=\"followups\">{string.Join(string.Empty, chunk.Select((entry, itemIndex) => FollowUpRow(entry.Kind, entry.Item, offset + itemIndex)))}</ul>";
            offset += chunk.Length;
            var heading = index == 0 ? "Medidas correctivas y recomendaciones" : "Medidas correctivas y recomendaciones (continuación)";
            return $"{PageStart(assets, data, heading, "Acciones consignadas durante la evaluación")}{Title("4", heading)}{body}{Footer(data, startPage + index)}</section>";
        }));
    }

    private static string FollowUpRow(string kind, EvaluationFollowUpItem item, int index) =>
        $"<li><b>{index + 1:00}</b><span><strong>{E(kind)}</strong><br>{E(item.Detail)}</span><time>{E(item.DueDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Sin fecha")}</time></li>";

    private static List<ReportEvidence[]> SplitEvidence(IReadOnlyList<ReportEvidence> evidences)
    {
        if (evidences.Count == 0) return [Array.Empty<ReportEvidence>()];

        var chunks = new List<ReportEvidence[]> { evidences.Take(7).ToArray() };
        chunks.AddRange(evidences.Skip(7).Chunk(9));
        return chunks;
    }

    private static string EvidencePages(ReportGenerationData data, InstitutionalReportAssets assets, int startPage,
        IReadOnlyList<ReportEvidence[]> chunks)
    {
        var offset = 0;
        return string.Concat(chunks.Select((chunk, index) =>
        {
            var identity = index == 0
                ? $"<div class=\"evidence-identity\">{Logos(assets)}<span><b style=\"font-size:9px\">Cadena documental verificable</b><small style=\"display:block;color:#5c6b66;font-size:7px;margin-top:3px\">Registro, asociación y conservación de evidencias</small></span></div><div class=\"process-flow\">{ProcessStep("01", "Captura", "Archivo original")}<span class=\"flow-arrow\">→</span>{ProcessStep("02", "Asociación", "Evaluación y hallazgo")}<span class=\"flow-arrow\">→</span>{ProcessStep("03", "Custodia", "Expediente trazable")}</div>"
                : string.Empty;
            var items = chunk.Length == 0
                ? "<div class=\"empty\"><strong>No se adjuntaron evidencias.</strong></div>"
                : $"<ul class=\"evidence\">{string.Join(string.Empty, chunk.Select((item, itemIndex) => EvidenceRow(item, offset + itemIndex)))}</ul>";
            offset += chunk.Length;
            var heading = index == 0 ? "Anexo de evidencias" : "Anexo de evidencias (continuación)";
            return $"{PageStart(assets, data, heading, "Índice documental asociado")}{identity}<p style=\"font-size:9px;color:#5c6b66\">Los archivos originales se incorporan a continuación y conservan su formato.</p>{items}{Footer(data, startPage + index)}</section>";
        }));
    }
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
    private static string Number(decimal? value, string suffix = "") => value.HasValue ? value.Value.ToString("0.00", CultureInfo.InvariantCulture) + suffix : "No calculable";
    private static string CompactNumber(decimal? value, string suffix = "") => value.HasValue ? value.Value.ToString("0.#", CultureInfo.InvariantCulture) + suffix : "N/D";
    private static string Percent(decimal? value) => Math.Clamp(value ?? 0, 0, 100).ToString("0.##", CultureInfo.InvariantCulture);
    private static string RiskPercent(decimal? value) => Math.Clamp(((value ?? 1m) - 1m) / 8m * 100m, 1m, 99m).ToString("0.##", CultureInfo.InvariantCulture);
    private static string DateRange(ReportGenerationData d) => $"{d.StartedAt?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? "No registrada"} - {d.FinishedAt?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? "En curso"}";
}
