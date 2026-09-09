using SIGERSA.Domain.Risk;

namespace SIGERSA.Tests;

public sealed class RiskEngineTests
{
    [Fact]
    public void CalculateBpmShouldApplyTheConfiguredScores()
    {
        BpmRating[] ratings =
        [
            BpmRating.Compliant,
            BpmRating.PartiallyCompliant,
            BpmRating.NonCompliant
        ];

        var result = RiskEngine.CalculateBpm(ratings);

        Assert.True(result.IsCalculable);
        Assert.Equal(0.5m, result.Score);
    }

    [Fact]
    public void CalculateBpmShouldExcludeNotApplicableItemsFromDenominator()
    {
        BpmRating[] ratings =
        [
            BpmRating.Compliant,
            BpmRating.PartiallyCompliant,
            BpmRating.NotApplicable,
            BpmRating.NotApplicable
        ];

        var result = RiskEngine.CalculateBpm(ratings);

        Assert.True(result.IsCalculable);
        Assert.Equal(0.75m, result.Score);
    }

    [Fact]
    public void CalculateBpmShouldBeNonCalculableWhenEveryItemIsNotApplicable()
    {
        var result = RiskEngine.CalculateBpm(
            [BpmRating.NotApplicable, BpmRating.NotApplicable]);

        Assert.False(result.IsCalculable);
        Assert.Null(result.Score);
    }

    [Fact]
    public void CalculateProductRiskShouldUseHighestSubcategoryRisk()
    {
        var result = RiskEngine.CalculateProductRisk([1m, 3m, 2m]);

        Assert.Equal(3m, result);
    }

    [Fact]
    public void CalculateProductRiskShouldBeNonCalculableWhenASubcategoryHasNoRisk()
    {
        var result = RiskEngine.CalculateProductRisk([1m, null, 3m]);

        Assert.Null(result);
    }

    [Fact]
    public void CalculateProductRiskShouldBeNonCalculableWithoutSubcategories()
    {
        var result = RiskEngine.CalculateProductRisk([]);

        Assert.Null(result);
    }

    [Fact]
    public void CalculateEstablishmentRiskShouldSumWeightedFactorScores()
    {
        RiskFactor[] factors =
        [
            new(0.50m, 3m),
            new(0.30m, 2m),
            new(0.20m, 1m)
        ];

        var result = RiskEngine.CalculateEstablishmentRisk(factors);

        Assert.Equal(2.30m, result);
    }

    [Fact]
    public void CalculateEstablishmentRiskShouldBeNonCalculableWhenAFactorHasNoScore()
    {
        RiskFactor[] factors = [new(0.50m, 3m), new(0.50m, null)];

        var result = RiskEngine.CalculateEstablishmentRisk(factors);

        Assert.Null(result);
    }

    [Fact]
    public void CalculateEstablishmentRiskShouldRejectWeightsThatDoNotSumOne()
    {
        RiskFactor[] factors = [new(0.40m, 3m), new(0.40m, 2m)];

        Assert.Throws<ArgumentException>(() =>
            RiskEngine.CalculateEstablishmentRisk(factors));
    }

    [Fact]
    public void CalculateTotalRiskShouldMultiplyProductAndEstablishmentRisks()
    {
        var result = RiskEngine.CalculateTotalRisk(3m, 2m);

        Assert.True(result.IsCalculable);
        Assert.Equal(6m, result.TotalRisk);
        Assert.Equal(RiskLevel.Medium, result.Level);
        Assert.Equal(InspectionFrequency.Semiannual, result.Frequency);
    }

    [Theory]
    [InlineData("1.0", "1.0", "1.0", RiskLevel.Low, InspectionFrequency.Annual)]
    [InlineData("1.2", "3.0", "3.6", RiskLevel.Low, InspectionFrequency.Annual)]
    [InlineData("1.2001", "3.0", "3.6003", RiskLevel.Medium, InspectionFrequency.Semiannual)]
    [InlineData("2.1", "3.0", "6.3", RiskLevel.Medium, InspectionFrequency.Semiannual)]
    [InlineData("2.1001", "3.0", "6.3003", RiskLevel.High, InspectionFrequency.Quarterly)]
    [InlineData("3.0", "3.0", "9.0", RiskLevel.High, InspectionFrequency.Quarterly)]
    public void CalculateTotalRiskShouldRespectRangeBoundaries(
        string productRiskText,
        string establishmentRiskText,
        string totalRiskText,
        RiskLevel expectedLevel,
        InspectionFrequency expectedFrequency)
    {
        var productRisk = decimal.Parse(productRiskText, System.Globalization.CultureInfo.InvariantCulture);
        var establishmentRisk = decimal.Parse(establishmentRiskText, System.Globalization.CultureInfo.InvariantCulture);
        var totalRisk = decimal.Parse(totalRiskText, System.Globalization.CultureInfo.InvariantCulture);

        var result = RiskEngine.CalculateTotalRisk(productRisk, establishmentRisk);

        Assert.Equal(totalRisk, result.TotalRisk);
        Assert.Equal(expectedLevel, result.Level);
        Assert.Equal(expectedFrequency, result.Frequency);
    }

    [Theory]
    [InlineData(null, "2.0")]
    [InlineData("3.0", null)]
    public void CalculateTotalRiskShouldBeNonCalculableWhenAComponentIsMissing(
        string? productRiskText,
        string? establishmentRiskText)
    {
        var productRisk = ParseNullableDecimal(productRiskText);
        var establishmentRisk = ParseNullableDecimal(establishmentRiskText);

        var result = RiskEngine.CalculateTotalRisk(productRisk, establishmentRisk);

        Assert.False(result.IsCalculable);
        Assert.Null(result.TotalRisk);
        Assert.Equal(RiskLevel.NoCalculable, result.Level);
        Assert.Equal(InspectionFrequency.NotApplicable, result.Frequency);
    }

    private static decimal? ParseNullableDecimal(string? value) => value is null
        ? null
        : decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
