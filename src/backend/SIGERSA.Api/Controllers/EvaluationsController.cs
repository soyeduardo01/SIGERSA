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
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR,TECNICO_EVALUADOR")]
    public Task<EvaluationsPage> Search(
        string? search, string? status, int page = 1, int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        service.SearchAsync(search, status, page, pageSize, ActorContext(), cancellationToken);

    [HttpGet("evaluations/options")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR,TECNICO_EVALUADOR")]
    public Task<EvaluationCreateOptions> Options(CancellationToken cancellationToken) =>
        service.GetOptionsAsync(ActorContext(), cancellationToken);

    [HttpPost("inspection-templates/publish-all-items")]
    [Authorize(Policy = "Administrator")]
    public Task<PublishedInspectionTemplate> Publish(CancellationToken cancellationToken) =>
        service.PublishAllItemsAsync(Actor(), cancellationToken);

    [HttpPost("evaluations")]
    [Authorize(Roles = "COORDINADOR")]
    public async Task<ActionResult<EvaluationSession>> Create(
        CreateEvaluationDraft request,
        CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, Actor(), cancellationToken);
        return CreatedAtAction(nameof(GetForm), new { evaluationId = created.Id }, created);
    }

    [HttpGet("evaluations/{evaluationId:guid}/form")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR,TECNICO_EVALUADOR")]
    public Task<IReadOnlyList<EvaluationFormItem>> GetForm(Guid evaluationId, CancellationToken cancellationToken) =>
        service.GetFormAsync(evaluationId, Actor(), cancellationToken);

    [HttpGet("evaluations/{evaluationId:guid}/workspace")]
    [Authorize(Roles = "ADMINISTRADOR,COORDINADOR,TECNICO_EVALUADOR")]
    public Task<EvaluationWorkspace> GetWorkspace(Guid evaluationId, CancellationToken cancellationToken) =>
        service.GetWorkspaceAsync(evaluationId, Actor(), cancellationToken);

    [HttpPut("evaluations/{evaluationId:guid}/supplement")]
    [Authorize(Roles = "TECNICO_EVALUADOR")]
    public Task<EvaluationSupplement> SaveSupplement(
        Guid evaluationId,
        SaveEvaluationSupplementDraft request,
        CancellationToken cancellationToken) =>
        service.SaveSupplementAsync(evaluationId, request, ActorContext(), cancellationToken);

    [HttpPost("respuestas")]
    [Authorize(Roles = "TECNICO_EVALUADOR")]
    public Task<EvaluationAnswer> SaveAnswer(SaveAnswerRequest request, CancellationToken cancellationToken) =>
        SaveAnswerCore(request.EvaluationId, request, cancellationToken);

    [HttpPost("evaluations/{evaluationId:guid}/answers")]
    [Authorize(Roles = "TECNICO_EVALUADOR")]
    public Task<EvaluationAnswer> SaveEvaluationAnswer(
        Guid evaluationId, SaveAnswerRequest request, CancellationToken cancellationToken) =>
        SaveAnswerCore(evaluationId, request, cancellationToken);

    private Task<EvaluationAnswer> SaveAnswerCore(
        Guid evaluationId, SaveAnswerRequest request, CancellationToken cancellationToken)
    {
        if (request.EvaluationId != Guid.Empty && request.EvaluationId != evaluationId)
            throw new ArgumentException("La evaluación de la ruta no coincide con la respuesta enviada.");
        if (!int.TryParse(request.ItemId, out var sourceItem))
        {
            throw new ArgumentException("ItemId debe ser el identificador numérico de AllItems.");
        }
        var idempotencyKey = request.IdempotencyKey ?? HeaderIdempotencyKey();
        return service.SaveAnswerAsync(new SaveEvaluationAnswerDraft(
            evaluationId,
            sourceItem,
            request.Value,
            request.CriticalityCode,
            request.Observation,
            request.Comment,
            idempotencyKey,
            request.DeviceId ?? idempotencyKey,
            request.ClientSequence ?? 0,
            request.ClientDate ?? DateTimeOffset.UtcNow,
            request.BaseVersion), ActorContext(), cancellationToken);
    }

    [HttpPost("evaluations/{evaluationId:guid}/calculate")]
    [Authorize(Roles = "TECNICO_EVALUADOR")]
    public Task<EvaluationCalculation> Calculate(
        Guid evaluationId,
        CancellationToken cancellationToken) =>
        service.CalculateAsync(evaluationId, ActorContext(), cancellationToken);

    [HttpPost("evaluations/{evaluationId:guid}/start")]
    [Authorize(Roles = "TECNICO_EVALUADOR")]
    public Task<long> Start(
        Guid evaluationId, StartEvaluationRequest request, CancellationToken cancellationToken) =>
        service.StartAsync(evaluationId,
            new EvaluationTransitionDraft("START", request.RowVersion, request.Latitude, request.Longitude, request.AccuracyMeters),
            ActorContext(), cancellationToken);

    [HttpPost("evaluations/{evaluationId:guid}/finalize")]
    [Authorize(Roles = "TECNICO_EVALUADOR")]
    public Task<EvaluationCalculation> Finalize(
        Guid evaluationId, CancellationToken cancellationToken) =>
        service.FinalizeAsync(evaluationId, ActorContext(), cancellationToken);

    [HttpPost("evaluations/{evaluationId:guid}/submit")]
    [Authorize(Roles = "TECNICO_EVALUADOR")]
    public Task<long> Submit(
        Guid evaluationId, EvaluationTransitionRequest request,
        CancellationToken cancellationToken) =>
        service.TransitionAsync(evaluationId,
            new EvaluationTransitionDraft("SUBMIT", request.RowVersion),
            ActorContext(), cancellationToken);

    [HttpPost("evaluations/{evaluationId:guid}/review")]
    [Authorize(Roles = "COORDINADOR")]
    public Task<long> Review(
        Guid evaluationId, EvaluationTransitionRequest request,
        CancellationToken cancellationToken) =>
        service.TransitionAsync(evaluationId,
            new EvaluationTransitionDraft("REVIEW", request.RowVersion),
            ActorContext(), cancellationToken);

    [HttpPost("evaluations/{evaluationId:guid}/approve")]
    [Authorize(Roles = "COORDINADOR")]
    public Task<long> Approve(
        Guid evaluationId, EvaluationTransitionRequest request,
        CancellationToken cancellationToken) =>
        service.TransitionAsync(evaluationId,
            new EvaluationTransitionDraft("APPROVE", request.RowVersion),
            ActorContext(), cancellationToken);

    [HttpPost("evaluations/{evaluationId:guid}/reject")]
    [Authorize(Roles = "COORDINADOR")]
    public Task<long> Reject(
        Guid evaluationId, EvaluationTransitionRequest request,
        CancellationToken cancellationToken) =>
        service.TransitionAsync(evaluationId,
            new EvaluationTransitionDraft("REJECT", request.RowVersion),
            ActorContext(), cancellationToken);

    [HttpPost("evaluations/{evaluationId:guid}/close")]
    [Authorize(Roles = "COORDINADOR")]
    public Task<long> Close(
        Guid evaluationId, EvaluationTransitionRequest request,
        CancellationToken cancellationToken) =>
        service.TransitionAsync(evaluationId,
            new EvaluationTransitionDraft("CLOSE", request.RowVersion),
            ActorContext(), cancellationToken);

    [HttpPost("evaluations/{evaluationId:guid}/cancel")]
    [Authorize(Roles = "COORDINADOR")]
    public Task<long> Cancel(
        Guid evaluationId, CancelEvaluationRequest request,
        CancellationToken cancellationToken) =>
        service.TransitionAsync(evaluationId,
            new EvaluationTransitionDraft("CANCEL", request.RowVersion, Reason: request.Reason),
            ActorContext(), cancellationToken);

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
    string? CriticalityCode,
    string? Observation,
    string? Comment,
    Guid? IdempotencyKey,
    Guid? DeviceId,
    long? ClientSequence,
    DateTimeOffset? ClientDate,
    long? BaseVersion);

public sealed record StartEvaluationRequest(long RowVersion, decimal? Latitude, decimal? Longitude, decimal? AccuracyMeters);
public sealed record EvaluationTransitionRequest(long RowVersion);
public sealed record CancelEvaluationRequest(long RowVersion, string Reason);
