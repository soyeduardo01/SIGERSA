using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SIGERSA.Application.Operations;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
public sealed class ReportsController(ReportService service) : ControllerBase
{
    [HttpPost("evaluations/{evaluationId:guid}/generate")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR")]
    public Task<ReportFileReference> Generate(
        Guid evaluationId, GenerateReportRequest request, CancellationToken cancellationToken) =>
        service.GenerateAsync(evaluationId, request.Official, Actor(), cancellationToken);

    [HttpGet("{reportId:guid}/content")]
    [Authorize(Roles = "ADMINISTRADOR,USUARIO_DELEGADO,COORDINADOR,TECNICO_EVALUADOR")]
    public async Task<IActionResult> Download(Guid reportId, CancellationToken cancellationToken)
    {
        var download = await service.DownloadAsync(reportId, Actor(), cancellationToken);
        return File(download.Content, download.MimeType, download.FileName, enableRangeProcessing: true);
    }

    [HttpGet("verify/{verificationToken}")]
    [AllowAnonymous]
    [EnableRateLimiting("report-verification")]
    public async Task<IActionResult> Verify(
        string verificationToken,
        CancellationToken cancellationToken)
    {
        var verification = await service.VerifyAsync(verificationToken, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return new ContentResult
        {
            StatusCode = verification is null ? StatusCodes.Status404NotFound : StatusCodes.Status200OK,
            ContentType = "text/html; charset=utf-8",
            Content = ReportVerificationPage.Build(verification)
        };
    }

    private OperationalActor Actor()
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var userId = Guid.TryParse(subject, out var parsedUserId)
            ? parsedUserId
            : throw new UnauthorizedAccessException();
        var companyId = Guid.TryParse(User.FindFirstValue("company_id"), out var parsedCompanyId)
            ? parsedCompanyId
            : (Guid?)null;
        var roles = User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new OperationalActor(userId, roles, companyId);
    }
}

public sealed record GenerateReportRequest(bool Official = false);

internal static class ReportVerificationPage
{
    public static string Build(ReportVerification? report)
    {
        if (report is null) return Shell("Informe no encontrado", "inválido", "No válido",
            "El código consultado no corresponde a un informe registrado en SIGERSA.", string.Empty);

        var annulled = report.ReportStatus.Equals("ANULADO", StringComparison.OrdinalIgnoreCase);
        var statusClass = annulled ? "inválido" : report.IsOfficial ? "válido" : "borrador";
        var heading = annulled ? "Informe anulado" : report.IsOfficial ? "Informe oficial válido" : "Borrador registrado";
        var detail = annulled
            ? "Este documento fue anulado y no debe utilizarse como informe vigente."
            : report.IsOfficial
                ? "El documento está registrado como una versión oficial emitida por SIGERSA."
                : "El documento existe, pero todavía no acredita una emisión oficial.";
        var issued = report.IssuedAt?.ToString("dd/MM/yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture)
            ?? "No emitido";
        var fields = $"""
            <dl>
              <div><dt>Informe</dt><dd>{E(report.ReportNumber)}</dd></div>
              <div><dt>Versión</dt><dd>{report.Version}</dd></div>
              <div><dt>Evaluación</dt><dd>{E(report.EvaluationNumber)}</dd></div>
              <div><dt>Empresa</dt><dd>{E(report.CompanyName)}</dd></div>
              <div><dt>Establecimiento</dt><dd>{E(report.EstablishmentName)}</dd></div>
              <div><dt>Emisión</dt><dd>{issued}</dd></div>
            </dl>
            <div class="hash"><span>Huella SHA-256 del archivo</span><code>{E(report.Sha256Hash)}</code></div>
            """;
        return Shell("Verificación de informe", statusClass, heading, detail, fields);
    }

    private static string Shell(string title, string statusClass, string heading, string detail, string fields)
    {
        const string template = """
        <!doctype html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>__TITLE__ · SIGERSA</title><style>
        *{box-sizing:border-box}body{margin:0;min-height:100vh;display:grid;place-items:center;padding:24px;background:#eef3f0;color:#132420;font-family:Arial,sans-serif}.card{width:min(680px,100%);overflow:hidden;border-radius:18px;background:#fff;box-shadow:0 18px 55px rgba(15,68,54,.15)}header{padding:28px 32px;background:#0f4436;color:#fff}header b{font-size:24px}header span{display:block;margin-top:5px;color:#bfe0d2;font-size:13px}.body{padding:32px}.badge{display:inline-flex;align-items:center;gap:8px;padding:8px 12px;border-radius:99px;font-weight:700;font-size:13px}.badge::before{content:'✓';display:grid;place-items:center;width:20px;height:20px;border-radius:50%;background:currentColor;color:#fff}.badge.válido{background:#eaf5e4;color:#176b4f}.badge.borrador{background:#fff4d7;color:#8a5d00}.badge.borrador::before{content:'!'}.badge.inválido{background:#fde9e7;color:#a82f25}.badge.inválido::before{content:'×'}h1{margin:18px 0 8px;color:#0f4436;font-size:28px}.lead{margin:0 0 26px;color:#5c6b66;line-height:1.6}dl{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin:0}dl div{padding:14px;border:1px solid #dce6e1;border-radius:9px;background:#f7faf8}dt,.hash span{color:#5c6b66;font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.5px}dd{margin:6px 0 0;font-weight:700}.hash{margin-top:16px;padding:14px;border-left:4px solid #238066;background:#f3f7f5}.hash span,.hash code{display:block}.hash code{margin-top:8px;color:#175a47;font-size:11px;overflow-wrap:anywhere}footer{padding:18px 32px;border-top:1px solid #dce6e1;color:#5c6b66;font-size:12px}@media(max-width:560px){dl{grid-template-columns:1fr}.body,header{padding:24px}h1{font-size:23px}}
        </style></head><body><main class="card"><header><b>SIGERSA</b><span>Sistema Integral de Gestión de Riesgo y Seguridad Alimentaria</span></header><section class="body"><span class="badge __STATUS_CLASS__">__HEADING__</span><h1>__TITLE__</h1><p class="lead">__DETAIL__</p>__FIELDS__</section><footer>Consulta pública de autenticidad documental · DIGEMAPS</footer></main></body></html>
        """;
        return template
            .Replace("__STATUS_CLASS__", statusClass, StringComparison.Ordinal)
            .Replace("__HEADING__", E(heading), StringComparison.Ordinal)
            .Replace("__TITLE__", E(title), StringComparison.Ordinal)
            .Replace("__DETAIL__", E(detail), StringComparison.Ordinal)
            .Replace("__FIELDS__", fields, StringComparison.Ordinal);
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
