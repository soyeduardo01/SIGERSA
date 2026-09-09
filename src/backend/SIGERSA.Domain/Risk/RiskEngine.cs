namespace SIGERSA.Domain.Risk;

public enum BpmRating
{
    Compliant,
    PartiallyCompliant,
    NonCompliant,
    NotApplicable
}

public enum RiskLevel
{
    NoCalculable,
    Low,
    Medium,
    High
}

public enum InspectionFrequency
{
    NotApplicable,
    Annual,
    Semiannual,
    Quarterly
}

public readonly record struct RiskFactor(decimal Weight, decimal? Score);

public sealed record BpmResult(decimal? Score, bool IsCalculable);

public sealed record RiskResult(
    decimal? ProductRisk,
    decimal? EstablishmentRisk,
    decimal? TotalRisk,
    RiskLevel Level,
    InspectionFrequency Frequency)
{
    public bool IsCalculable => Level is not RiskLevel.NoCalculable;
}

public static class RiskEngine
{
    private const decimal WeightTolerance = 0.0001m;

    public static BpmResult CalculateBpm(IEnumerable<BpmRating> ratings)
    {
        ArgumentNullException.ThrowIfNull(ratings);

        var applicableScores = ratings
            .Where(rating => rating is not BpmRating.NotApplicable)
            .Select(ToBpmScore)
            .ToArray();

        return applicableScores.Length == 0
            ? new BpmResult(null, false)
            : new BpmResult(applicableScores.Average(), true);
    }

    public static decimal? CalculateProductRisk(IEnumerable<decimal?> subcategoryRisks)
    {
        ArgumentNullException.ThrowIfNull(subcategoryRisks);

        var risks = subcategoryRisks.ToArray();
        if (risks.Length == 0 || risks.Any(risk => risk is null))
        {
            return null;
        }

        ValidateRiskScores(risks.Select(risk => risk!.Value), nameof(subcategoryRisks));
        return risks.Max()!.Value;
    }

    public static decimal? CalculateEstablishmentRisk(IEnumerable<RiskFactor> factors)
    {
        ArgumentNullException.ThrowIfNull(factors);

        var factorList = factors.ToArray();
        if (factorList.Length == 0 || factorList.Any(factor => factor.Score is null))
        {
            return null;
        }

        if (factorList.Any(factor => factor.Weight < 0m))
        {
            throw new ArgumentOutOfRangeException(nameof(factors), "Los pesos no pueden ser negativos.");
        }

        var totalWeight = factorList.Sum(factor => factor.Weight);
        if (Math.Abs(totalWeight - 1m) > WeightTolerance)
        {
            throw new ArgumentException("Los pesos activos deben sumar 1.00.", nameof(factors));
        }

        ValidateRiskScores(factorList.Select(factor => factor.Score!.Value), nameof(factors));
        return factorList.Sum(factor => factor.Weight * factor.Score!.Value);
    }

    public static RiskResult CalculateTotalRisk(decimal? productRisk, decimal? establishmentRisk)
    {
        if (productRisk is null || establishmentRisk is null)
        {
            return new RiskResult(
                productRisk,
                establishmentRisk,
                null,
                RiskLevel.NoCalculable,
                InspectionFrequency.NotApplicable);
        }

        ValidateRiskScores([productRisk.Value, establishmentRisk.Value], nameof(productRisk));

        var totalRisk = productRisk.Value * establishmentRisk.Value;
        var (level, frequency) = totalRisk switch
        {
            <= 3.6m => (RiskLevel.Low, InspectionFrequency.Annual),
            <= 6.3m => (RiskLevel.Medium, InspectionFrequency.Semiannual),
            _ => (RiskLevel.High, InspectionFrequency.Quarterly)
        };

        return new RiskResult(productRisk, establishmentRisk, totalRisk, level, frequency);
    }

    private static decimal ToBpmScore(BpmRating rating) => rating switch
    {
        BpmRating.Compliant => 1m,
        BpmRating.PartiallyCompliant => 0.5m,
        BpmRating.NonCompliant => 0m,
        _ => throw new ArgumentOutOfRangeException(nameof(rating), rating, "Calificación BPM no soportada.")
    };

    private static void ValidateRiskScores(IEnumerable<decimal> scores, string parameterName)
    {
        if (scores.Any(score => score is < 1m or > 3m))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Los puntajes de riesgo deben estar entre 1 y 3.");
        }
    }
}
