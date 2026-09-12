using SIGERSA.Domain.Entities;

namespace SIGERSA.Application.Evaluations;

public static class InspectionQualificationPolicy
{
    public const string FullForm = "FICHA_COMPLETA";
    public const string FromPoint113 = "DESDE_1_1_3";
    public const string PreviousNonconformities = "NO_CONFORMIDADES_ANTERIORES";
    public const string ComplaintDiscretion = "DENUNCIA_DISCRECIONAL";

    public static EvaluationInspectionPolicy Resolve(
        EvaluationInspectionContext context,
        IReadOnlyList<EvaluationFormItem> items,
        IReadOnlyCollection<int> answeredSourceItems)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(answeredSourceItems);

        var evaluable = items.Where(item => item.IsEvaluable).OrderBy(item => item.Order).ToArray();
        var allSources = evaluable.Select(item => item.SourceItem).Distinct().ToArray();
        var reason = context.InspectionReasonCode?.Trim().ToUpperInvariant() ?? string.Empty;
        var approvalPurpose = ApprovalPurpose(reason);

        if (context.Origin.Equals("DENUNCIA", StringComparison.OrdinalIgnoreCase) || reason.Contains("DENUNCIA", StringComparison.Ordinal))
        {
            var selected = evaluable
                .Where(item => answeredSourceItems.Contains(item.SourceItem))
                .Select(item => item.SourceItem)
                .Distinct()
                .ToArray();
            return Build(
                ComplaintDiscretion,
                "Inspección por denuncia",
                "El inspector decide el alcance y debe concentrarse en los aspectos relacionados con la denuncia. Los ítems sin respuesta no son obligatorios ni intervienen en el cálculo.",
                selected.Length > 0,
                selected.Length > 0 ? null : "Responda al menos un ítem relacionado con la denuncia antes de calcular o finalizar.",
                selected,
                allSources.Except(selected).ToArray(),
                null,
                context);
        }

        if (IsFollowUpOrControl(reason))
        {
            var required = evaluable
                .Where(item => context.PreviousNonconformingSourceItems.Contains(item.SourceItem))
                .Select(item => item.SourceItem)
                .Distinct()
                .ToArray();
            var ready = context.PreviousEvaluationId.HasValue && required.Length > 0;
            var blocking = context.PreviousEvaluationId is null
                ? "No existe una inspección anterior finalizada para realizar el seguimiento."
                : required.Length == 0
                    ? "La inspección anterior no contiene no conformidades que deban verificarse."
                    : null;
            return Build(
                PreviousNonconformities,
                "Inspección de seguimiento o control",
                "Solo se muestran, exigen y califican las no conformidades detectadas en la inspección anterior.",
                ready,
                blocking,
                required,
                allSources.Except(required).ToArray(),
                null,
                context);
        }

        if (context.Origin.Equals("PROGRAMACION", StringComparison.OrdinalIgnoreCase) && context.HasValidSanitaryPermit)
        {
            var cutoff = items.FirstOrDefault(item => string.Equals(item.Code.Trim(), "1.1.3", StringComparison.OrdinalIgnoreCase));
            if (cutoff is null)
            {
                return Build(
                    FromPoint113,
                    "Inspección programada con Permiso Sanitario",
                    "La ficha debe comenzar en el punto 1.1.3 y prestar especial atención a los aspectos críticos.",
                    false,
                    "La ficha publicada no contiene el punto 1.1.3. Corrija la plantilla antes de continuar.",
                    [],
                    allSources,
                    null,
                    context);
            }

            var required = evaluable.Where(item => item.Order >= cutoff.Order).Select(item => item.SourceItem).Distinct().ToArray();
            return Build(
                FromPoint113,
                "Inspección programada con Permiso Sanitario",
                "Solo son obligatorios y calificables los puntos desde 1.1.3 en adelante. Los puntos anteriores quedan fuera del cálculo.",
                required.Length > 0,
                required.Length > 0 ? null : "No hay ítems evaluables desde el punto 1.1.3.",
                required,
                allSources.Except(required).ToArray(),
                null,
                context);
        }

        return Build(
            FullForm,
            approvalPurpose is null ? "Inspección con ficha completa" : "Solicitud, renovación o certificación",
            approvalPurpose is null
                ? "Todos los ítems evaluables de la ficha son obligatorios y forman parte del cálculo."
                : "Debe completarse toda la ficha. La aprobación exige al menos 81 %, ninguna no conformidad crítica y menos de tres no conformidades mayores.",
            allSources.Length > 0,
            allSources.Length > 0 ? null : "La ficha publicada no contiene ítems evaluables.",
            allSources,
            [],
            approvalPurpose,
            context);
    }

    public static EvaluationDecisionGuidance Evaluate(
        EvaluationInspectionPolicy policy,
        decimal compliancePercentage,
        IReadOnlyCollection<EvaluationAnswer> applicableAnswers)
    {
        var critical = applicableAnswers.Count(answer => string.Equals(answer.CriticalityCode, "C", StringComparison.OrdinalIgnoreCase));
        var major = applicableAnswers.Count(answer => string.Equals(answer.CriticalityCode, "M", StringComparison.OrdinalIgnoreCase));
        var minor = applicableAnswers.Count(answer => string.Equals(answer.CriticalityCode, "ME", StringComparison.OrdinalIgnoreCase));
        var messages = new List<string>();

        string band;
        string condition;
        string recommendation;
        if (compliancePercentage <= 60m)
        {
            band = "HASTA_60";
            condition = "Condiciones inaceptables";
            recommendation = "Considerar el cierre del establecimiento.";
        }
        else if (compliancePercentage <= 70m)
        {
            band = "MAS_60_HASTA_70";
            condition = "Condiciones deficientes";
            recommendation = "Urge corregir las no conformidades.";
        }
        else if (compliancePercentage <= 80m)
        {
            band = "MAS_70_HASTA_80";
            condition = "Condiciones regulares";
            recommendation = "Es necesario realizar correcciones.";
        }
        else
        {
            band = "MAS_80";
            condition = "Buenas condiciones";
            recommendation = "Realizar las correcciones que correspondan.";
        }

        if (compliancePercentage < 81m)
            messages.Add("Notificar las no conformidades detectadas y establecer fechas para corregirlas.");
        if (compliancePercentage <= 60m)
            messages.Add("Considerar la posibilidad de cerrar el establecimiento.");
        if (critical > 0)
            messages.Add("Se detectó una no conformidad crítica: recomendar detener la producción hasta corregirla.");

        bool? approvalEligible = null;
        if (policy.ApprovalPurpose is not null)
        {
            approvalEligible = compliancePercentage >= 81m && critical == 0 && major < 3;
            messages.Add(approvalEligible.Value
                ? ApprovalSuccess(policy.ApprovalPurpose)
                : "No cumple las condiciones para aprobación: se requiere al menos 81 %, ninguna no conformidad crítica y menos de tres mayores.");
        }

        return new EvaluationDecisionGuidance(
            band,
            condition,
            recommendation,
            approvalEligible,
            applicableAnswers.Count,
            critical,
            major,
            minor,
            messages);
    }

    private static EvaluationInspectionPolicy Build(
        string mode,
        string title,
        string explanation,
        bool ready,
        string? blockingReason,
        IReadOnlyList<int> required,
        IReadOnlyList<int> excluded,
        string? approvalPurpose,
        EvaluationInspectionContext context) =>
        new(
            mode,
            title,
            explanation,
            ready,
            blockingReason,
            required,
            excluded,
            approvalPurpose,
            context.PreviousEvaluationId,
            context.PreviousInspectionDate,
            context.PreviousCompliancePercentage);

    private static bool IsFollowUpOrControl(string reason) =>
        reason.Contains("SEGUIMIENTO", StringComparison.Ordinal) ||
        reason.Contains("CONTROL", StringComparison.Ordinal) ||
        reason.Contains("VIGILANCIA", StringComparison.Ordinal);

    private static string? ApprovalPurpose(string reason) => reason switch
    {
        "SOLICITUD_PERMISO_SANITARIO" => "OTORGAR_PERMISO_SANITARIO",
        "RENOVACION_PERMISO_SANITARIO" => "RENOVAR_PERMISO_SANITARIO",
        "CERTIFICACION_BPM" or "SOLICITUD_CERTIFICACION_BPM" => "OTORGAR_CERTIFICACION_BPM",
        _ => null
    };

    private static string ApprovalSuccess(string purpose) => purpose switch
    {
        "RENOVAR_PERMISO_SANITARIO" => "Cumple las condiciones para renovar el Permiso Sanitario.",
        "OTORGAR_CERTIFICACION_BPM" => "Cumple las condiciones para otorgar la Certificación BPM.",
        _ => "Cumple las condiciones para otorgar el Permiso Sanitario."
    };
}
