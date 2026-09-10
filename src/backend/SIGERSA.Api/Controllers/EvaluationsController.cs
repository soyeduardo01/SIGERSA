using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIGERSA.Application.Evaluations;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class EvaluationsController(EvaluationWorkflowService service) : ControllerBase
{
    [HttpGet("evaluations")]
    [Authorize(Roles = "ADMINISTRADOR,ADMINISTRADOR_EMPRESA,USUARIO_DELEGADO,COORDINADOR,TECNICO_EVALUADOR")]
    public Task<EvaluationsPage> Search(
        string? search, string? status, int page = 1, int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        service.SearchAsync(search, status, page, pageSize, ActorContext(), cancellationToken);

    [HttpGet("evaluations/options")]
    [Authorize(Roles = "ADMINISTRADOR,ADMINISTRADOR_EMPRESA,USUARIO_DELEGADO,COORDINADOR,TECNICO_EVALUADOR")]
    public Task<EvaluationCreateOptions> Options(CancellationToken cancellationToken) =>
        service.GetOptionsAsync(ActorContext(), cancellationToken);

    [HttpPost("inspection-templates/publish-all-items")]
    [Authorize(Policy = "Administrator")]
    public Task<PublishedInspectionTemplate> Publish(CancellationToken cancellationToken) =>
        service.PublishAllItemsAsync(Actor(), cancellationToken);

    [HttpPost("evaluations")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR")]
    public async Task<ActionResult<EvaluationSession>> Create(
        CreateEvaluationDraft request,
        CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, Actor(), cancellationToken);
        return CreatedAtAction(nameof(GetForm), new { evaluationId = created.Id }, created);
    }

    [HttpGet("evaluations/{evaluationId:guid}/form")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR,TECNICO_EVALUADOR,ADMINISTRADOR_EMPRESA,USUARIO_DELEGADO")]
    public Task<IReadOnlyList<EvaluationFormItem>> GetForm(Guid evaluationId, CancellationToken cancellationToken) =>
        service.GetFormAsync(evaluationId, Actor(), cancellationToken);

    [HttpPost("respuestas")]
    [Authorize(Roles = "ADMINISTRADOR,TECNICO_EVALUADOR")]
    public Task<EvaluationAnswer> SaveAnswer(SaveAnswerRequest request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(request.ItemId, out var sourceItem))
        {
            throw new ArgumentException("ItemId debe ser el identificador numérico de AllItems.");
        }
        var idempotencyKey = request.IdempotencyKey ?? HeaderIdempotencyKey();
        return service.SaveAnswerAsync(new SaveEvaluationAnswerDraft(
            request.EvaluationId,
            sourceItem,
            request.Value,
            request.Observation,
            idempotencyKey,
            request.DeviceId ?? idempotencyKey,
            request.ClientSequence ?? 0,
            request.ClientDate ?? DateTimeOffset.UtcNow,
            request.BaseVersion), Actor(), cancellationToken);
    }

    [HttpPost("evaluations/{evaluationId:guid}/calculate")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR,TECNICO_EVALUADOR")]
    public Task<EvaluationCalculation> Calculate(
        Guid evaluationId,
        CalculateEvaluationRequest request,
        CancellationToken cancellationToken) =>
        service.CalculateAsync(evaluationId, request.ProductRisk, Actor(), cancellationToken);

    private Guid Actor()
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(subject, out var id) ? id : throw new UnauthorizedAccessException();
    }

    private EvaluationActor ActorContext()
    {
        var userId = Actor();
        var companyId = Guid.TryParse(User.FindFirstValue("company_id"), out var parsedCompanyId)
            ? parsedCompanyId
            : (Guid?)null;
        var roles = User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new EvaluationActor(userId, roles, companyId);
    }

    private Guid HeaderIdempotencyKey()
    {
        var value = Request.Headers["Idempotency-Key"].ToString();
        return Guid.TryParse(value, out var id)
            ? id
            : throw new ArgumentException("Se requiere un Idempotency-Key UUID.");
    }
}

public sealed record SaveAnswerRequest(
    Guid EvaluationId,
    string ItemId,
    string Value,
    string? Observation,
    Guid? IdempotencyKey,
    Guid? DeviceId,
    long? ClientSequence,
    DateTimeOffset? ClientDate,
    long? BaseVersion);

public sealed record CalculateEvaluationRequest(decimal ProductRisk = 1m);
