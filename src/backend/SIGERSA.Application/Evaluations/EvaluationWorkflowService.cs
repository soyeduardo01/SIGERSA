using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Risk;

namespace SIGERSA.Application.Evaluations;

public sealed class EvaluationWorkflowService(IEvaluationWorkflowRepository repository)
{
    public Task<EvaluationsPage> SearchAsync(
        string? search, string? status, int page, int pageSize,
        EvaluationActor actor, CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        var global = HasRole(actor, "ADMINISTRADOR") || HasRole(actor, "COORDINADOR");
        var assigned = HasRole(actor, "TECNICO_EVALUADOR") && !global;
        var company = global || assigned ? (Guid?)null : actor.CompanyId
            ?? throw new ForbiddenException("El usuario no tiene un ámbito empresarial válido.");
        return repository.SearchAsync(new EvaluationSearch(
            Normalize(search)?.ToLowerInvariant(), Normalize(status)?.ToUpperInvariant(),
            Math.Max(page, 1), Math.Clamp(pageSize, 5, 100), actor.UserId,
            company, global, assigned), cancellationToken);
    }

    public Task<EvaluationCreateOptions> GetOptionsAsync(EvaluationActor actor, CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        return repository.GetOptionsAsync(HasRole(actor, "ADMINISTRADOR") || HasRole(actor, "COORDINADOR"), cancellationToken);
    }

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
        var productionScore = ScoreProduction(input.MonthlyProduction);
        var haccpScore = ScoreHaccp(input.HaccpImplemented, input.HaccpPercentage);
        var bpmScore = ScoreBpm(compliance);
        var inabieScore = ScoreInabie(input.IsInabieSupplier, input.InabieDistributionCode);
        var rejectionScore = ScoreRejections(input.MicrobiologicalRejectionsLastFiveYears);
        var samplingScore = ScoreSampling(input.MicrobiologicalSamplingPlan, input.SamplingApplicationCode);
        var establishmentRisk = RiskEngine.CalculateEstablishmentRisk([
            new RiskFactor(0.16m, productionScore),
            new RiskFactor(0.09m, haccpScore),
            new RiskFactor(0.56m, bpmScore),
            new RiskFactor(0.05m, inabieScore),
            new RiskFactor(0.06m, rejectionScore),
            new RiskFactor(0.08m, samplingScore)
        ]);
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
            factors = new { productionScore, haccpScore, bpmScore, inabieScore, rejectionScore, samplingScore },
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

    private static decimal? ScoreProduction(decimal? monthlyProduction) => monthlyProduction switch
    {
        null => null,
        > 2_000_000m => 3m,
        >= 800_000m => 2.33m,
        >= 200_000m => 1.67m,
        _ => 1m
    };

    private static decimal? ScoreHaccp(bool? implemented, decimal? percentage) => implemented switch
    {
        null => null,
        false => 3m,
        true when percentage <= 25m => 2.33m,
        true when percentage <= 75m => 1.67m,
        true => 1m
    };

    private static decimal? ScoreBpm(decimal? compliance) => compliance switch
    {
        null => null,
        <= 81m => 3m,
        <= 89m => 2.33m,
        <= 95m => 1.67m,
        _ => 1m
    };

    private static decimal? ScoreInabie(bool? supplier, string? distribution) => supplier switch
    {
        null => null,
        false => 1m,
        true when distribution == "NACIONAL" => 3m,
        true when distribution == "REGIONAL" => 2.33m,
        true when distribution == "LOCAL" => 1.67m,
        _ => null
    };

    private static decimal ScoreRejections(int count) => count switch
    {
        > 2 => 3m,
        2 => 2.33m,
        1 => 1.67m,
        _ => 1m
    };

    private static decimal? ScoreSampling(bool? hasPlan, string? application) => hasPlan switch
    {
        null => null,
        false => 3m,
        true when application == "MATERIAS_PRIMAS" => 2.33m,
        true when application == "AREAS_PROCESO_PRODUCTOS_TERMINADOS" => 1.67m,
        true when application == "MATERIAS_PRIMAS_AREAS_PROCESO_PRODUCTOS_TERMINADOS" => 1m,
        _ => null
    };

    private static Guid Required(Guid value, string name)
    {
        if (value == Guid.Empty) throw new ArgumentException("El identificador es obligatorio.", name);
        return value;
    }

    private static void EnsureReader(EvaluationActor actor)
    {
        if (!actor.Roles.Any(role => role is "ADMINISTRADOR" or "ADMINISTRADOR_EMPRESA" or "USUARIO_DELEGADO" or "COORDINADOR" or "TECNICO_EVALUADOR"))
            throw new ForbiddenException("No tiene permisos para consultar evaluaciones.");
    }

    private static bool HasRole(EvaluationActor actor, string role) =>
        actor.Roles.Contains(role, StringComparer.Ordinal);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record EvaluationActor(Guid UserId, string[] Roles, Guid? CompanyId);
