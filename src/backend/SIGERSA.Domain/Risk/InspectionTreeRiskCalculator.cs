using SIGERSA.Domain.Entities;

namespace SIGERSA.Domain.Risk;

public sealed record AllItemRating(int Item, BpmRating Rating);

public sealed record AllItemScore(int Item, string ItemsId, decimal? Score, bool IsCalculable);

public static class InspectionTreeRiskCalculator
{
    public static IReadOnlyList<AllItemScore> Calculate(
        IEnumerable<AllItem> items,
        IEnumerable<AllItemRating> ratings)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(ratings);
        var allItems = items.OrderBy(item => item.Items).ToArray();
        var ratingByItem = ratings.ToDictionary(rating => rating.Item, rating => rating.Rating);
        var childrenByParent = allItems
            .Where(item => item.Parents is not null)
            .GroupBy(item => item.Parents!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var results = new Dictionary<int, AllItemScore>();
        var visiting = new HashSet<int>();

        foreach (var item in allItems)
        {
            CalculateNode(item, childrenByParent, ratingByItem, results, visiting);
        }

        return allItems.Select(item => results[item.Items]).ToArray();
    }

    private static AllItemScore CalculateNode(
        AllItem item,
        IReadOnlyDictionary<string, AllItem[]> childrenByParent,
        IReadOnlyDictionary<int, BpmRating> ratingByItem,
        IDictionary<int, AllItemScore> results,
        ISet<int> visiting)
    {
        if (results.TryGetValue(item.Items, out var cached)) return cached;
        if (!visiting.Add(item.Items))
        {
            throw new InvalidOperationException("La estructura AllItems contiene un ciclo.");
        }

        AllItemScore result;
        if (string.Equals(item.SectionType, "I", StringComparison.OrdinalIgnoreCase))
        {
            var bpm = ratingByItem.TryGetValue(item.Items, out var rating)
                ? RiskEngine.CalculateBpm([rating])
                : new BpmResult(null, false);
            result = new AllItemScore(item.Items, item.ItemsId, bpm.Score, bpm.IsCalculable);
        }
        else
        {
            var childScores = childrenByParent.TryGetValue(item.ItemsId, out var children)
                ? children.Select(child => CalculateNode(child, childrenByParent, ratingByItem, results, visiting))
                    .Where(score => score.IsCalculable)
                    .Select(score => score.Score!.Value)
                    .ToArray()
                : [];
            result = childScores.Length == 0
                ? new AllItemScore(item.Items, item.ItemsId, null, false)
                : new AllItemScore(item.Items, item.ItemsId, childScores.Average(), true);
        }

        visiting.Remove(item.Items);
        results[item.Items] = result;
        return result;
    }
}
