using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Risk;

namespace SIGERSA.Application.Risk;

public sealed record InspectionRatingRequest(int Item, string Rating);

public sealed class InspectionRiskService(IAllItemsRepository repository)
{
    public async Task<IReadOnlyList<AllItemScore>> CalculateAsync(
        IReadOnlyCollection<InspectionRatingRequest> ratings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ratings);
        if (ratings.Select(rating => rating.Item).Distinct().Count() != ratings.Count)
        {
            throw new ArgumentException("Cada ítem puede tener una sola calificación.", nameof(ratings));
        }

        var items = await repository.GetAllAsync(cancellationToken);
        var itemById = items.ToDictionary(item => item.Items);
        var mapped = ratings.Select(rating =>
        {
            if (!itemById.TryGetValue(rating.Item, out var item))
            {
                throw new ArgumentException($"El ítem {rating.Item} no existe.", nameof(ratings));
            }

            if (!string.Equals(item.SectionType, "I", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"El ítem {rating.Item} no es una pregunta evaluable.", nameof(ratings));
            }

            return new AllItemRating(rating.Item, ParseRating(rating.Rating));
        }).ToArray();

        return InspectionTreeRiskCalculator.Calculate(items, mapped);
    }

    private static BpmRating ParseRating(string value) => value.Trim().ToUpperInvariant() switch
    {
        "C" or "CUMPLE" => BpmRating.Compliant,
        "CP" or "CUMPLE_PARCIAL" or "CUMPLE PARCIAL" => BpmRating.PartiallyCompliant,
        "IT" or "NO_CUMPLE" or "NO CUMPLE" or "INCUMPLIMIENTO" => BpmRating.NonCompliant,
        "NA" or "N/A" or "NO_APLICA" or "NO APLICA" => BpmRating.NotApplicable,
        _ => throw new ArgumentException($"La calificación '{value}' no está soportada.", nameof(value))
    };
}
