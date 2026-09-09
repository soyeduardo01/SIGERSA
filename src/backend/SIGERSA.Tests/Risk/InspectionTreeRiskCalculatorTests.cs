using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Risk;

namespace SIGERSA.Tests.Risk;

public sealed class InspectionTreeRiskCalculatorTests
{
    [Fact]
    public void CalculateShouldAggregateLeafRatingsRecursively()
    {
        AllItem[] items =
        [
            new(1, "1", "Categoría", "C", null),
            new(2, "1.1", "Sección", "S", "1"),
            new(3, "1.1.1", "Cumple", "I", "1.1"),
            new(4, "1.1.2", "No cumple", "I", "1.1")
        ];
        AllItemRating[] ratings =
        [
            new(3, BpmRating.Compliant),
            new(4, BpmRating.NonCompliant)
        ];

        var result = InspectionTreeRiskCalculator.Calculate(items, ratings);

        Assert.Equal(0.5m, result.Single(score => score.Item == 2).Score);
        Assert.Equal(0.5m, result.Single(score => score.Item == 1).Score);
    }

    [Fact]
    public void CalculateShouldIgnoreNotApplicableLeaves()
    {
        AllItem[] items =
        [
            new(1, "1", "Categoría", "C", null),
            new(2, "1.1", "Aplica", "I", "1"),
            new(3, "1.2", "No aplica", "I", "1")
        ];

        var result = InspectionTreeRiskCalculator.Calculate(items,
            [new(2, BpmRating.PartiallyCompliant), new(3, BpmRating.NotApplicable)]);

        Assert.Equal(0.5m, result.Single(score => score.Item == 1).Score);
    }

    [Fact]
    public void CalculateShouldRejectCycles()
    {
        AllItem[] items =
        [
            new(1, "A", "A", "S", "B"),
            new(2, "B", "B", "S", "A")
        ];

        Assert.Throws<InvalidOperationException>(() =>
            InspectionTreeRiskCalculator.Calculate(items, []));
    }
}
