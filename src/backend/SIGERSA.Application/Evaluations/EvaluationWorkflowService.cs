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
    private static readonly HashSet<string> QualificationOptions = new(StringComparer.Ordinal)
    {
        "CONSIDERAR_CIERRE",
        "URGE_CORREGIR",
        "NECESARIO_CORREGIR",
        "ALGUNAS_CORRECCIONES",
        "DETENER_PRODUCCION",
        "CORREGIR_NO_CONFORMIDADES",
        "OTORGAR_CERTIFICACION_BPM",
        "RENOVAR_PERMISO_SANITARIO",
        "OTORGAR_PERMISO_SANITARIO"
    };

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
            company, global, assigned, HasRole(actor, "TECNICO_EVALUADOR")), cancellationToken);
    }

    public Task<EvaluationCreateOptions> GetOptionsAsync(EvaluationActor actor, CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        return repository.GetOptionsAsync(HasRole(actor, "COORDINADOR"), cancellationToken);
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

    public async Task<EvaluationWorkspace> GetWorkspaceAsync(
        Guid evaluationId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var id = Required(evaluationId, nameof(evaluationId));
        var userId = Required(actorId, nameof(actorId));
        var workspace = await repository.GetWorkspaceAsync(id, userId, cancellationToken);
        var context = await repository.GetInspectionContextAsync(id, userId, cancellationToken);
        var calculation = await repository.GetCalculationInputAsync(id, userId, cancellationToken);
        var policy = InspectionQualificationPolicy.Resolve(
            context,
            workspace.Items,
            workspace.Answers.Select(answer => answer.SourceItem).ToHashSet());
        return new EvaluationWorkspace(
            workspace.Items, workspace.Answers, workspace.Evidences, workspace.Supplement, policy,
            new EvaluationCalculationContext(
                calculation.ProductRisk, calculation.MonthlyProduction,
                calculation.HaccpImplemented, calculation.HaccpPercentage,
                calculation.IsInabieSupplier, calculation.InabieDistributionCode,
                calculation.MicrobiologicalRejectionsLastFiveYears,
                calculation.MicrobiologicalSamplingPlan, calculation.SamplingApplicationCode));
    }

    public Task<EvaluationSupplement> SaveSupplementAsync(
        Guid evaluationId,
        SaveEvaluationSupplementDraft draft,
        EvaluationActor actor,
        CancellationToken cancellationToken)
    {
        if (!HasRole(actor, "TECNICO_EVALUADOR"))
            throw new ForbiddenException("Solo el técnico evaluador puede completar los datos de la evaluación.");
        if (draft.RowVersion < 0)
            throw new ArgumentException("La versión de los datos complementarios no es válida.");
        if (draft.PreviousInspectionDate > draft.CurrentInspectionDate)
            throw new ArgumentException("La inspección anterior no puede ser posterior a la inspección actual.");

        var normalized = draft with
        {
            PreviousQualification = NormalizeQualification(draft.PreviousQualification),
            CurrentQualification = NormalizeQualification(draft.CurrentQualification),
            DpsDasOfficer1 = NormalizeLimited(draft.DpsDasOfficer1, 200, "Oficial DPS/DAS"),
            DpsDasOfficer2 = NormalizeLimited(draft.DpsDasOfficer2, 200, "Oficial DPS/DAS"),
            DigemapsTechnician1 = NormalizeLimited(draft.DigemapsTechnician1, 200, "Técnico DIGEMAPS"),
            DigemapsTechnician2 = NormalizeLimited(draft.DigemapsTechnician2, 200, "Técnico DIGEMAPS"),
            CorrectiveMeasures = NormalizeFollowUps(draft.CorrectiveMeasures, "medidas correctivas"),
            Recommendations = NormalizeFollowUps(draft.Recommendations, "recomendaciones")
        };
        return repository.SaveSupplementAsync(
            Required(evaluationId, nameof(evaluationId)), normalized, actor.UserId, cancellationToken);
    }

    public Task<EvaluationAnswer> SaveAnswerAsync(SaveEvaluationAnswerDraft draft, EvaluationActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!HasRole(actor, "TECNICO_EVALUADOR"))
            throw new ForbiddenException("Solo el técnico evaluador puede registrar respuestas.");
        Required(draft.EvaluationId, nameof(draft.EvaluationId));
        Required(draft.IdempotencyKey, nameof(draft.IdempotencyKey));
        Required(draft.DeviceId, nameof(draft.DeviceId));
        if (draft.SourceItem <= 0) throw new ArgumentOutOfRangeException(nameof(draft), "El elemento de origen debe ser positivo.");
        if (draft.ClientSequence < 0) throw new ArgumentOutOfRangeException(nameof(draft), "La secuencia del cliente no puede ser negativa.");
        var normalized = NormalizeRating(draft.Rating);
        var criticality = Normalize(draft.CriticalityCode)?.ToUpperInvariant();
        if (normalized == "NO_CUMPLE" && criticality is not ("C" or "M" or "ME"))
            throw new ArgumentException("Seleccione la criticidad C, M o Me para un incumplimiento total.");
        if (normalized != "NO_CUMPLE") criticality = null;
        return repository.SaveAnswerAsync(
            draft with { Rating = normalized, CriticalityCode = criticality },
            Required(actor.UserId, nameof(actor.UserId)), cancellationToken);
    }

    public async Task<EvaluationCalculation> CalculateAsync(
        Guid evaluationId,
        EvaluationActor actor,
        CancellationToken cancellationToken)
    {
        if (!HasRole(actor, "TECNICO_EVALUADOR"))
            throw new ForbiddenException("Solo el técnico evaluador puede calcular la evaluación.");
        var input = await repository.GetCalculationInputAsync(
            Required(evaluationId, nameof(evaluationId)),
            Required(actor.UserId, nameof(actor.UserId)),
            cancellationToken);
        var context = await repository.GetInspectionContextAsync(evaluationId, actor.UserId, cancellationToken);
        var policy = InspectionQualificationPolicy.Resolve(
            context,
            input.Items,
            input.Answers.Select(answer => answer.SourceItem).ToHashSet());
        if (!policy.IsReady)
            throw new InvalidOperationException(policy.BlockingReason ?? "El alcance de la inspección no está listo para calcularse.");
        var applicableSources = policy.RequiredSourceItems.ToHashSet();
        var applicableAnswers = input.Answers
            .Where(answer => applicableSources.Contains(answer.SourceItem))
            .GroupBy(answer => answer.SourceItem)
            .Select(group => group.First())
            .ToArray();
        var missingAnswers = policy.RequiredSourceItems.Count - applicableAnswers.Length;
        if (missingAnswers > 0)
            throw new InvalidOperationException($"Faltan {missingAnswers} de {policy.RequiredSourceItems.Count} ítems obligatorios para este tipo de inspección.");
        if (applicableAnswers.Length == 0)
            throw new InvalidOperationException("Debe responder al menos un ítem aplicable antes de calcular el riesgo.");
        var ratings = applicableAnswers.Select(answer => ParseRating(answer.Rating)).ToArray();
        var bpm = RiskEngine.CalculateBpm(ratings);
        var applicableScore = ratings.Count(rating => rating is not BpmRating.NotApplicable);
        var nodes = input.Items.Select(item => new AllItem(
            item.SourceItem,
            item.Id.ToString("N"),
            item.Title,
            item.IsEvaluable ? "I" : "S",
            item.ParentId?.ToString("N"))).ToArray();
        var nodeRatings = applicableAnswers.Select(answer =>
            new AllItemRating(answer.SourceItem, ParseRating(answer.Rating))).ToArray();
        var nodeScores = InspectionTreeRiskCalculator.Calculate(nodes, nodeRatings);
        var compliance = bpm.Score.HasValue
            ? bpm.Score.Value * 100m
            : throw new InvalidOperationException("Todos los ítems aplicables están marcados como N/A; no es posible calcular una calificación.");
        var obtainedScore = bpm.Score.Value * applicableScore;
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
        var decision = InspectionQualificationPolicy.Evaluate(policy, compliance, applicableAnswers);
        var total = RiskEngine.CalculateTotalRisk(input.ProductRisk, establishmentRisk);
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
        var nextInspectionDate = total.Frequency switch
        {
            InspectionFrequency.Annual => DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(12),
            InspectionFrequency.Semiannual => DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(6),
            InspectionFrequency.Quarterly => DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(3),
            _ => (DateOnly?)null
        };
        var factors = new[]
        {
            Factor("PRODUCCION", "Volumen de producción", 0.16m, productionScore,
                input.MonthlyProduction is null ? "Sin producción mensual registrada" : $"{input.MonthlyProduction:N0} por mes"),
            Factor("HACCP", "Implementación del sistema HACCP", 0.09m, haccpScore,
                input.HaccpImplemented == true ? $"Implementado al {input.HaccpPercentage ?? 0m:N1} %" : "No implementado"),
            Factor("BPM", "Cumplimiento con las BPM", 0.56m, bpmScore,
                $"Cumplimiento calculado: {compliance:N1} %"),
            Factor("INABIE", "Proveedor INABIE", 0.05m, inabieScore,
                input.IsInabieSupplier == true ? $"Distribución: {input.InabieDistributionCode ?? "sin especificar"}" : "No es suplidor INABIE"),
            Factor("RECHAZOS", "Rechazos microbiológicos", 0.06m, rejectionScore,
                $"{input.MicrobiologicalRejectionsLastFiveYears} en los últimos 5 años"),
            Factor("MUESTREO", "Plan de muestreo", 0.08m, samplingScore,
                input.MicrobiologicalSamplingPlan == true ? $"Aplicación: {input.SamplingApplicationCode ?? "sin especificar"}" : "No dispone de plan")
        };
        var breakdown = new RiskCalculationBreakdown(
            "Riesgo total = riesgo microbiológico del producto × riesgo del establecimiento",
            total.TotalRisk,
            total.TotalRisk,
            null,
            factors);
        var calculation = new EvaluationCalculation(
            input.EvaluationId,
            compliance,
            total.ProductRisk,
            total.EstablishmentRisk,
            total.TotalRisk,
            level,
            frequency,
            nextInspectionDate,
            decision,
            breakdown,
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
            calculation.NextInspectionDate,
            breakdown,
            policy = new
            {
                policy.Mode,
                policy.Title,
                policy.RequiredSourceItems,
                policy.ExcludedSourceItems,
                policy.ApprovalPurpose,
                policy.PreviousEvaluationId
            },
            calculation.Decision,
            scores = new { obtained = obtainedScore, applicable = applicableScore },
            factors = new { productionScore, haccpScore, bpmScore, inabieScore, rejectionScore, samplingScore },
            nodes = nodeScores
        });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))).ToLowerInvariant();
        var version = await repository.SaveCalculationAsync(
            calculation, obtainedScore, applicableScore, snapshot, hash, input.RowVersion, actor.UserId, cancellationToken);
        return calculation with { RowVersion = version };
    }

    public async Task<long> StartAsync(
        Guid evaluationId,
        EvaluationTransitionDraft transition,
        EvaluationActor actor,
        CancellationToken cancellationToken)
    {
        if (!HasRole(actor, "TECNICO_EVALUADOR"))
            throw new ForbiddenException("Solo el técnico evaluador puede iniciar la evaluación.");
        ValidateCoordinates(transition);
        return await TransitionAsync(evaluationId, transition with { Action = "START" }, actor, cancellationToken);
    }

    public async Task<EvaluationCalculation> FinalizeAsync(
        Guid evaluationId,
        EvaluationActor actor,
        CancellationToken cancellationToken)
    {
        if (!HasRole(actor, "TECNICO_EVALUADOR"))
            throw new ForbiddenException("Solo el técnico evaluador puede finalizar la evaluación.");
        var calculation = await CalculateAsync(evaluationId, actor, cancellationToken);
        if (calculation.TotalRisk is null)
            throw new InvalidOperationException("La evaluación no puede finalizar hasta completar las respuestas y factores de riesgo requeridos.");
        var rowVersion = await TransitionAsync(evaluationId,
            new EvaluationTransitionDraft("FINALIZE", calculation.RowVersion), actor, cancellationToken);
        return calculation with { RowVersion = rowVersion };
    }

    public async Task<long> TransitionAsync(
        Guid evaluationId,
        EvaluationTransitionDraft transition,
        EvaluationActor actor,
        CancellationToken cancellationToken)
    {
        if (transition.RowVersion <= 0) throw new ArgumentException("La versión de la evaluación es obligatoria.");
        var action = transition.Action.Trim().ToUpperInvariant();
        var executionAction = action is "START" or "FINALIZE";
        var submitAction = action == "SUBMIT";
        var reviewerAction = action is "REVIEW" or "APPROVE" or "REJECT" or "CLOSE" or "CANCEL";
        if (!executionAction && !submitAction && !reviewerAction) throw new ArgumentException("La transición solicitada no es válida.");
        if (executionAction && !HasRole(actor, "TECNICO_EVALUADOR"))
            throw new ForbiddenException("La transición corresponde al técnico evaluador asignado.");
        if (submitAction && !HasRole(actor, "TECNICO_EVALUADOR"))
            throw new ForbiddenException("Solo el técnico evaluador puede enviar la evaluación a revisión.");
        if (reviewerAction && !HasRole(actor, "COORDINADOR"))
            throw new ForbiddenException("La transición corresponde al coordinador revisor.");
        if (action == "CANCEL")
        {
            var reason = NormalizeLimited(transition.Reason, 2000, "El motivo de cancelación")
                ?? throw new ArgumentException("El motivo de cancelación es obligatorio.");
            transition = transition with { Reason = reason };
        }
        if (action == "APPROVE")
            await EnsureApprovalCriteriaAsync(evaluationId, actor.UserId, cancellationToken);
        if (action == "REJECT")
            await EnsureRejectionCriteriaAsync(evaluationId, actor.UserId, cancellationToken);
        var version = await repository.TransitionAsync(
            evaluationId, transition with { Action = action }, actor.UserId, cancellationToken);
        return version ?? throw new OptimisticConcurrencyException(evaluationId);
    }

    private async Task EnsureApprovalCriteriaAsync(
        Guid evaluationId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var input = await repository.GetCalculationInputAsync(evaluationId, actorId, cancellationToken);
        var context = await repository.GetInspectionContextAsync(evaluationId, actorId, cancellationToken);
        var policy = InspectionQualificationPolicy.Resolve(
            context,
            input.Items,
            input.Answers.Select(answer => answer.SourceItem).ToHashSet());
        if (policy.ApprovalPurpose is null) return;
        if (!policy.IsReady)
            throw new InvalidOperationException(policy.BlockingReason ?? "El alcance de la inspección no está listo para aprobarse.");
        var required = policy.RequiredSourceItems.ToHashSet();
        var answers = input.Answers.Where(answer => required.Contains(answer.SourceItem)).ToArray();
        if (answers.Length != required.Count)
            throw new InvalidOperationException("La evaluación no contiene todas las respuestas obligatorias para aprobarse.");
        var bpm = RiskEngine.CalculateBpm(answers.Select(answer => ParseRating(answer.Rating)));
        var compliance = bpm.Score.HasValue
            ? bpm.Score.Value * 100m
            : throw new InvalidOperationException("No se puede aprobar una evaluación sin ítems calificables.");
        var decision = InspectionQualificationPolicy.Evaluate(policy, compliance, answers);
        if (decision.ApprovalEligible != true)
            throw new InvalidOperationException("No se puede aprobar: se requiere al menos 81 %, ninguna no conformidad crítica y menos de tres no conformidades mayores.");
    }

    private async Task EnsureRejectionCriteriaAsync(
        Guid evaluationId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var input = await repository.GetCalculationInputAsync(evaluationId, actorId, cancellationToken);
        var context = await repository.GetInspectionContextAsync(evaluationId, actorId, cancellationToken);
        var policy = InspectionQualificationPolicy.Resolve(
            context, input.Items, input.Answers.Select(answer => answer.SourceItem).ToHashSet());
        if (policy.ApprovalPurpose is null)
            throw new InvalidOperationException("Esta evaluación no corresponde a una solicitud de aprobación o certificación.");
        var required = policy.RequiredSourceItems.ToHashSet();
        var answers = input.Answers.Where(answer => required.Contains(answer.SourceItem)).ToArray();
        if (answers.Length != required.Count)
            throw new InvalidOperationException("La evaluación no contiene todas las respuestas obligatorias.");
        var bpm = RiskEngine.CalculateBpm(answers.Select(answer => ParseRating(answer.Rating)));
        var compliance = bpm.Score.HasValue
            ? bpm.Score.Value * 100m
            : throw new InvalidOperationException("No se puede decidir una evaluación sin ítems calificables.");
        var decision = InspectionQualificationPolicy.Evaluate(policy, compliance, answers);
        if (decision.ApprovalEligible != false)
            throw new InvalidOperationException("La evaluación cumple las condiciones de aprobación y no puede marcarse como no aprobada.");
    }

    private static void ValidateCoordinates(EvaluationTransitionDraft transition)
    {
        if ((transition.Latitude.HasValue) != (transition.Longitude.HasValue))
            throw new ArgumentException("La latitud y longitud deben enviarse juntas.");
        if (transition.Latitude is < -90m or > 90m || transition.Longitude is < -180m or > 180m)
            throw new ArgumentException("Las coordenadas no son válidas.");
        if (transition.AccuracyMeters < 0m) throw new ArgumentException("La precisión no puede ser negativa.");
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
        true when application is "MP" or "MATERIAS_PRIMAS" => 2.33m,
        true when application is "AP_PT" or "AREAS_PROCESO_PRODUCTOS_TERMINADOS" => 1.67m,
        true when application is "MP_AP_PT" or "MATERIAS_PRIMAS_AREAS_PROCESO_PRODUCTOS_TERMINADOS" => 1m,
        _ => null
    };

    private static RiskFactorBreakdown Factor(
        string code, string name, decimal weight, decimal? score, string basis) =>
        new(code, name, weight, score, score * weight, basis);

    private static Guid Required(Guid value, string name)
    {
        if (value == Guid.Empty) throw new ArgumentException("El identificador es obligatorio.", name);
        return value;
    }

    private static string? NormalizeQualification(string? value)
    {
        var normalized = Normalize(value)?.ToUpperInvariant();
        if (normalized is not null && !QualificationOptions.Contains(normalized))
            throw new ArgumentException("La calificación seleccionada no es válida.");
        return normalized;
    }

    private static string? NormalizeLimited(string? value, int maximumLength, string field)
    {
        var normalized = Normalize(value);
        if (normalized?.Length > maximumLength)
            throw new ArgumentException($"{field} no puede exceder {maximumLength} caracteres.");
        return normalized;
    }

    private static EvaluationFollowUpItem[] NormalizeFollowUps(
        IReadOnlyList<EvaluationFollowUpItem>? values,
        string field)
    {
        var normalized = (values ?? [])
            .Select(value => value with { Detail = value.Detail.Trim() })
            .Where(value => value.Detail.Length > 0)
            .ToArray();
        if (normalized.Length > 10)
            throw new ArgumentException($"Solo se permiten hasta 10 {field}.");
        if (normalized.Any(value => value.Detail.Length > 2000))
            throw new ArgumentException($"El detalle de {field} no puede exceder 2000 caracteres.");
        return normalized;
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
