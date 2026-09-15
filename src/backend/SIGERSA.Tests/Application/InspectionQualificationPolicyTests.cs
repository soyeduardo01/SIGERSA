using SIGERSA.Application.Evaluations;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Tests.Application;

public sealed class InspectionQualificationPolicyTests
{
    private static readonly EvaluationFormItem[] Items =
    [
        Item(1, "1.1.1", 1),
        Item(2, "1.1.2", 2),
        Item(3, "1.1.3", 3),
        Item(4, "1.1.4", 4)
    ];

    [Theory]
    [InlineData("SOLICITUD_PERMISO_SANITARIO", "OTORGAR_PERMISO_SANITARIO")]
    [InlineData("RENOVACION_PERMISO_SANITARIO", "RENOVAR_PERMISO_SANITARIO")]
    [InlineData("CERTIFICACION_BPM", "OTORGAR_CERTIFICACION_BPM")]
    public void RequestRenewalAndCertificationRequireTheFullForm(string reason, string purpose)
    {
        var policy = InspectionQualificationPolicy.Resolve(Context("SOLICITUD_EMPRESA", reason), Items, []);

        Assert.Equal(InspectionQualificationPolicy.FullForm, policy.Mode);
        Assert.Equal(purpose, policy.ApprovalPurpose);
        Assert.Equal([1, 2, 3, 4], policy.RequiredSourceItems);
    }

    [Fact]
    public void ProgrammedInspectionWithPermitStartsAtPoint113()
    {
        var policy = InspectionQualificationPolicy.Resolve(
            Context("PROGRAMACION", null, hasPermit: true), Items, []);

        Assert.Equal(InspectionQualificationPolicy.FromPoint113, policy.Mode);
        Assert.Equal([3, 4], policy.RequiredSourceItems);
        Assert.Equal([1, 2], policy.ExcludedSourceItems);
    }

    [Fact]
    public void ProgrammedInspectionRecognizesPublishedAllItemsPointCode()
    {
        var items = new[]
        {
            Item(1, "AI-0008-1.1.2", 1),
            Item(2, "AI-0011-1.1.3", 2),
            Item(3, "AI-0012-1.1.3.1", 3)
        };

        var policy = InspectionQualificationPolicy.Resolve(
            Context("PROGRAMACION", null, hasPermit: true), items, []);

        Assert.True(policy.IsReady);
        Assert.Equal([2, 3], policy.RequiredSourceItems);
        Assert.Equal([1], policy.ExcludedSourceItems);
    }

    [Fact]
    public void ExplicitPermitRequestRequiresFullFormEvenWhenScheduled()
    {
        var policy = InspectionQualificationPolicy.Resolve(
            Context("PROGRAMACION", "SOLICITUD_PERMISO_SANITARIO", hasPermit: true), Items, []);

        Assert.Equal(InspectionQualificationPolicy.FullForm, policy.Mode);
        Assert.Equal([1, 2, 3, 4], policy.RequiredSourceItems);
    }

    [Fact]
    public void FollowUpUsesOnlyNonconformitiesFromThePreviousInspection()
    {
        var priorId = Guid.NewGuid();
        var policy = InspectionQualificationPolicy.Resolve(
            Context("PROGRAMACION", "VIGILANCIA_CONTROL_RUTINA", hasPermit: true,
                priorId: priorId, priorNonconformities: [2, 4]),
            Items,
            []);

        Assert.Equal(InspectionQualificationPolicy.PreviousNonconformities, policy.Mode);
        Assert.True(policy.IsReady);
        Assert.Equal([2, 4], policy.RequiredSourceItems);
    }

    [Fact]
    public void FollowUpIsBlockedWhenThereIsNoPreviousInspection()
    {
        var policy = InspectionQualificationPolicy.Resolve(
            Context("SOLICITUD_EMPRESA", "SEGUIMIENTO"), Items, []);

        Assert.False(policy.IsReady);
        Assert.Contains("anterior", policy.BlockingReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ComplaintLeavesScopeToTheInspectorAndRequiresAtLeastOneSelectedAnswer()
    {
        var empty = InspectionQualificationPolicy.Resolve(Context("DENUNCIA", null), Items, []);
        var selected = InspectionQualificationPolicy.Resolve(Context("DENUNCIA", null), Items, [2, 4]);

        Assert.False(empty.IsReady);
        Assert.Equal([2, 4], selected.RequiredSourceItems);
        Assert.True(selected.IsReady);
    }

    [Fact]
    public void ScoreBelow81NotifiesNonconformitiesAndScoreAtMost60ConsidersClosure()
    {
        var policy = InspectionQualificationPolicy.Resolve(Context("PROGRAMACION", null), Items, []);
        var decision = InspectionQualificationPolicy.Evaluate(policy, 60m, [Answer(1, "M")]);

        Assert.Equal("HASTA_60", decision.Band);
        Assert.Contains(decision.Messages, message => message.Contains("Notificar", StringComparison.Ordinal));
        Assert.Contains(decision.Messages, message => message.Contains("cerrar", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CriticalNonconformityStopsProductionAndPreventsPermitApproval()
    {
        var policy = InspectionQualificationPolicy.Resolve(
            Context("SOLICITUD_EMPRESA", "SOLICITUD_PERMISO_SANITARIO"), Items, []);
        var decision = InspectionQualificationPolicy.Evaluate(policy, 95m, [Answer(1, "C")]);

        Assert.False(decision.ApprovalEligible);
        Assert.Contains(decision.Messages, message => message.Contains("detener la producción", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void PermitApprovalAllowsFewerThanThreeMajorNonconformities(int majorCount, bool expected)
    {
        var policy = InspectionQualificationPolicy.Resolve(
            Context("SOLICITUD_EMPRESA", "SOLICITUD_PERMISO_SANITARIO"), Items, []);
        var answers = Enumerable.Range(1, majorCount).Select(source => Answer(source, "M")).ToArray();

        var decision = InspectionQualificationPolicy.Evaluate(policy, 81m, answers);

        Assert.Equal(expected, decision.ApprovalEligible);
    }

    private static EvaluationFormItem Item(int source, string code, int order) =>
        new(Guid.NewGuid(), null, source, code, code, true, 1, order);

    private static EvaluationAnswer Answer(int source, string criticality) =>
        new(Guid.NewGuid(), Guid.NewGuid(), source, "NO_CUMPLE", criticality, 0m, 1);

    private static EvaluationInspectionContext Context(
        string origin,
        string? reason,
        bool hasPermit = false,
        Guid? priorId = null,
        IReadOnlyList<int>? priorNonconformities = null) =>
        new(origin, reason, reason, hasPermit, priorId, priorId.HasValue ? new DateOnly(2026, 1, 1) : null,
            priorId.HasValue ? 75m : null, priorNonconformities ?? []);
}
