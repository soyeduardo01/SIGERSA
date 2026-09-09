using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Risk;

namespace SIGERSA.Application.Evaluations;

public sealed class EvaluationWorkflowService(IEvaluationWorkflowRepository repository)
{
    public Task<PublishedInspectionTemplate> PublishAllItemsAsync(Guid actorId, CancellationToken cancellationToken) =>
        repository.PublishAllItemsAsync(Required(actorId, nameof(actorId)), cancellationToken);

    public Task<EvaluationSession> CreateAsync(CreateEvaluationDraft draft, Guid actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        Required(draft.CaseId, nameof(draft.CaseId));
        Required(draft.EstablishmentId, nameof(draft.EstablishmentId));
        Required(draft.InspectionTemplateId, nameof(draft.InspectionTemplateId));
        Required(draft.EvaluatorId, nameof(draft.EvaluatorId));
        Required(draft.RiskRuleVersionId, nameof(draft.RiskRuleVersionId));
        if (draft.ScheduledEnd <= draft.ScheduledStart)
        {
            throw new ArgumentException("La fecha final programada debe ser posterior a la inicial.", nameof(draft));
        }
        return repository.CreateEvaluationAsync(draft, Required(actorId, nameof(actorId)), cancellationToken);
    }

    public Task<IReadOnlyList<EvaluationFormItem>> GetFormAsync(Guid evaluationId, Guid actorId, CancellationToken cancellationToken) =>
        repository.GetFormAsync(Required(evaluationId, nameof(evaluationId)), Required(actorId, nameof(actorId)), cancellationToken);

    public Task<EvaluationAnswer> SaveAnswerAsync(SaveEvaluationAnswerDraft draft, Guid actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        Required(draft.EvaluationId, nameof(draft.EvaluationId));
        Required(draft.IdempotencyKey, nameof(draft.IdempotencyKey));
        Required(draft.DeviceId, nameof(draft.DeviceId));
        if (draft.SourceItem <= 0) throw new ArgumentOutOfRangeException(nameof(draft), "El elemento de origen debe ser positivo.");
        if (draft.ClientSequence < 0) throw new ArgumentOutOfRangeException(nameof(draft), "La secuencia del cliente no puede ser negativa.");
        var normalized = NormalizeRating(draft.Rating);
        return repository.SaveAnswerAsync(draft with { Rating = normalized }, Required(actorId, nameof(actorId)), cancellationToken);
    }

    public async Task<EvaluationCalculation> CalculateAsync(
        Guid evaluationId,
        decimal productRisk,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        if (productRisk is < 1m or > 3m) throw new ArgumentOutOfRangeException(nameof(productRisk));
        var input = await repository.GetCalculationInputAsync(
            Required(evaluationId, nameof(evaluationId)),
            Required(actorId, nameof(actorId)),
            cancellationToken);
        var ratings = input.Answers.Select(answer => ParseRating(answer.Rating)).ToArray();
        var bpm = RiskEngine.CalculateBpm(ratings);
        var nodes = input.Items.Select(item => new AllItem(
            item.SourceItem,
            item.Id.ToString("N"),
            item.Title,
            item.IsEvaluable ? "I" : "S",
            item.ParentId?.ToString("N"))).ToArray();
        var nodeRatings = input.Answers.Select(answer =>
            new AllItemRating(answer.SourceItem, ParseRating(answer.Rating))).ToArray();
        var nodeScores = InspectionTreeRiskCalculator.Calculate(nodes, nodeRatings);
        var compliance = bpm.Score * 100m;
        decimal? establishmentRisk = bpm.Score is null ? null : 1m + ((1m - bpm.Score.Value) * 2m);
        var total = RiskEngine.CalculateTotalRisk(productRisk, establishmentRisk);
        var level = total.Level switch
        {
            RiskLevel.Low => "BAJO",
            RiskLevel.Medium => "MEDIO",
            RiskLevel.High => "ALTO",
            _ => "NO_CALCULABLE"
        };
        var frequency = total.Frequency switch
        {
            InspectionFrequency.Annual => "ANUAL",
            InspectionFrequency.Semiannual => "SEMESTRAL",
            InspectionFrequency.Quarterly => "TRIMESTRAL",
            _ => "NO_APLICA"
        };
        var calculation = new EvaluationCalculation(
            input.EvaluationId,
            compliance,
            total.ProductRisk,
            total.EstablishmentRisk,
            total.TotalRisk,
            level,
            frequency,
            input.RowVersion);
        var snapshot = JsonSerializer.Serialize(new
        {
            calculatedAt = DateTimeOffset.UtcNow,
            calculation.CompliancePercentage,
            calculation.ProductRisk,
            calculation.EstablishmentRisk,
            calculation.TotalRisk,
            calculation.RiskLevel,
            calculation.Frequency,
            nodes = nodeScores
        });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))).ToLowerInvariant();
        var version = await repository.SaveCalculationAsync(
            calculation, snapshot, hash, input.RowVersion, actorId, cancellationToken);
        return calculation with { RowVersion = version };
    }

    private static string NormalizeRating(string value) => ParseRating(value) switch
    {
        BpmRating.Compliant => "CUMPLE",
        BpmRating.PartiallyCompliant => "CUMPLE_PARCIAL",
        BpmRating.NonCompliant => "NO_CUMPLE",
        _ => "NO_APLICA"
    };

    private static BpmRating ParseRating(string value) => value.Trim().ToUpperInvariant() switch
    {
        "C" or "CUMPLE" => BpmRating.Compliant,
        "CP" or "CUMPLE_PARCIAL" => BpmRating.PartiallyCompliant,
        "IT" or "NO_CUMPLE" or "INCUMPLIMIENTO" => BpmRating.NonCompliant,
        "NA" or "N/A" or "NO_APLICA" => BpmRating.NotApplicable,
        _ => throw new ArgumentException($"La calificación '{value}' no está soportada.", nameof(value))
    };

    private static Guid Required(Guid value, string name)
    {
        if (value == Guid.Empty) throw new ArgumentException("El identificador es obligatorio.", name);
        return value;
    }
}
