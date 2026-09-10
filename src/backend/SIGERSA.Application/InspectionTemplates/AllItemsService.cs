using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Application.InspectionTemplates;

public sealed class AllItemsService(IAllItemsRepository repository)
{
    public const string DeleteSubtree = "SUBTREE";
    public const string ReparentChildren = "REPARENT";

    public Task<IReadOnlyList<AllItem>> GetAllAsync(CancellationToken cancellationToken) =>
        repository.GetAllAsync(cancellationToken);

    public async Task<AllItem> CreateAsync(AllItemDraft draft, CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(null, draft, cancellationToken);
        return await repository.CreateAsync(validation.Draft, cancellationToken);
    }

    public async Task UpdateAsync(int id, AllItemDraft draft, CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(id, draft, cancellationToken);
        if (!await repository.UpdateAsync(id, validation.Draft, validation.ChildrenToReparent, cancellationToken))
        {
            throw new KeyNotFoundException("El ítem de ficha no existe.");
        }
    }

    public async Task DeleteAsync(
        int id,
        string? childStrategy,
        int? reparentToItems,
        CancellationToken cancellationToken)
    {
        var items = await repository.GetAllAsync(cancellationToken);
        var current = items.SingleOrDefault(item => item.Items == id)
            ?? throw new KeyNotFoundException("El ítem de ficha no existe.");
        var parentIds = ResolveParentIds(items);
        var directChildren = items.Where(item => parentIds[item.Items] == id).ToArray();
        int[] itemsToDelete = [id];
        IReadOnlyDictionary<int, string?> childrenToReparent = new Dictionary<int, string?>();

        if (directChildren.Length > 0)
        {
            var strategy = childStrategy?.Trim().ToUpperInvariant();
            if (strategy == DeleteSubtree)
            {
                itemsToDelete = DescendantsOf(id, items, parentIds).Append(id).ToArray();
            }
            else if (strategy == ReparentChildren)
            {
                var descendants = DescendantsOf(id, items, parentIds).ToHashSet();
                var target = reparentToItems is null
                    ? parentIds[id] is int parentId ? items.Single(item => item.Items == parentId) : null
                    : items.SingleOrDefault(item => item.Items == reparentToItems)
                        ?? throw new KeyNotFoundException("El nuevo nodo padre no existe.");
                if (target?.Items == id || (target is not null && descendants.Contains(target.Items)))
                {
                    throw new InvalidOperationException("No se puede reubicar un hijo dentro del subárbol que será eliminado.");
                }
                if (target is not null && directChildren.Any(child => target.Items >= child.Items))
                {
                    throw new InvalidOperationException("El nuevo padre debe aparecer antes que sus hijos en la ficha.");
                }
                foreach (var child in directChildren)
                {
                    ValidateSectionPlacement(child.SectionType, target?.SectionType);
                }
                childrenToReparent = directChildren.ToDictionary(child => child.Items, _ => target?.ItemsId);
            }
            else
            {
                throw new InvalidOperationException(
                    "El nodo tiene hijos. Indique SUBTREE para eliminar el subárbol o REPARENT para reubicar los hijos.");
            }
        }

        if (await repository.ApplyDeleteAsync(itemsToDelete, childrenToReparent, cancellationToken) != itemsToDelete.Length)
        {
            throw new KeyNotFoundException("El ítem de ficha no existe o cambió durante la operación.");
        }
    }

    public async Task<IReadOnlyList<AllItem>> ReorderAsync(
        IReadOnlyList<int> orderedItems,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderedItems);
        if (orderedItems.Count == 0)
        {
            throw new ArgumentException("El nuevo orden no puede estar vacío.", nameof(orderedItems));
        }
        var items = await repository.GetAllAsync(cancellationToken);
        if (orderedItems.Count != items.Count || orderedItems.Distinct().Count() != items.Count)
        {
            throw new ArgumentException("El orden debe incluir cada nodo existente exactamente una vez.", nameof(orderedItems));
        }
        var byId = items.ToDictionary(item => item.Items);
        if (orderedItems.Any(id => !byId.ContainsKey(id)))
        {
            throw new ArgumentException("El orden contiene nodos inexistentes.", nameof(orderedItems));
        }
        var priorByCode = new Dictionary<string, AllItem>(StringComparer.Ordinal);
        foreach (var itemId in orderedItems)
        {
            var item = byId[itemId];
            AllItem? parent = null;
            if (item.Parents is not null && !priorByCode.TryGetValue(item.Parents, out parent))
            {
                throw new InvalidOperationException($"El nodo {item.ItemsId} debe aparecer después de su padre {item.Parents}.");
            }
            ValidateSectionPlacement(item.SectionType, parent?.SectionType);
            priorByCode[item.ItemsId] = item;
        }
        return await repository.ReorderAsync(orderedItems, cancellationToken);
    }

    private async Task<ValidatedDraft> ValidateAsync(int? currentId, AllItemDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.ItemsId);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Description);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.SectionType);
        var normalized = new AllItemDraft(
            draft.ItemsId.Trim(),
            draft.Description.Trim(),
            draft.SectionType.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(draft.Parents) ? null : draft.Parents.Trim());
        var items = await repository.GetAllAsync(cancellationToken);
        var current = currentId is null ? null : items.SingleOrDefault(item => item.Items == currentId)
            ?? throw new KeyNotFoundException("El ítem de ficha no existe.");
        var parentIds = ResolveParentIds(items);
        AllItem[] directChildren = current is null
            ? []
            : items.Where(item => parentIds[item.Items] == current.Items).ToArray();

        if (normalized.Parents is null)
        {
            ValidateSectionPlacement(normalized.SectionType, null);
        }
        else
        {
            if (string.Equals(normalized.ItemsId, normalized.Parents, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Un ítem no puede ser su propio padre.");
            }

            var candidates = items.Where(item => item.Items != currentId && item.ItemsId == normalized.Parents);
            if (current is not null) candidates = candidates.Where(item => item.Items < current.Items);
            var parent = candidates.OrderBy(item => item.Items).LastOrDefault()
                ?? throw new InvalidOperationException("El nodo padre indicado no existe antes del nodo actual.");
            ValidateSectionPlacement(normalized.SectionType, parent.SectionType);
            if (current is not null && DescendantsOf(current.Items, items, parentIds).Contains(parent.Items))
            {
                throw new InvalidOperationException("La jerarquía propuesta contiene un ciclo.");
            }
        }

        foreach (var child in directChildren)
        {
            ValidateSectionPlacement(child.SectionType, normalized.SectionType);
        }

        IReadOnlyCollection<int> childrenToReparent = current is not null && current.ItemsId != normalized.ItemsId
            ? directChildren.Select(child => child.Items).ToArray()
            : [];
        return new ValidatedDraft(normalized, childrenToReparent);
    }

    private static Dictionary<int, int?> ResolveParentIds(IReadOnlyList<AllItem> items)
    {
        var result = new Dictionary<int, int?>();
        var priorByCode = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in items.OrderBy(item => item.Items))
        {
            result[item.Items] = item.Parents is not null && priorByCode.TryGetValue(item.Parents, out var parentId)
                ? parentId
                : null;
            priorByCode[item.ItemsId] = item.Items;
        }
        return result;
    }

    private static HashSet<int> DescendantsOf(
        int parentId,
        IReadOnlyList<AllItem> items,
        Dictionary<int, int?> parentIds)
    {
        var descendants = new HashSet<int>();
        var pending = new Queue<int>();
        pending.Enqueue(parentId);
        while (pending.TryDequeue(out var current))
        {
            foreach (var child in items.Where(item => parentIds[item.Items] == current))
            {
                if (descendants.Add(child.Items)) pending.Enqueue(child.Items);
            }
        }
        return descendants;
    }

    private static void ValidateSectionPlacement(string sectionType, string? parentSectionType)
    {
        var child = sectionType.Trim().ToUpperInvariant();
        var parent = parentSectionType?.Trim().ToUpperInvariant();
        var valid = (child, parent) switch
        {
            ("C", null) => true,
            ("S", "C") => true,
            ("SS", "S") => true,
            ("A", "SS") => true,
            ("I", "C" or "S" or "SS" or "A") => true,
            _ => false
        };
        if (!valid)
        {
            throw new InvalidOperationException(
                $"La ubicación del tipo {child} bajo {parent ?? "RAÍZ"} no es compatible.");
        }
    }

    private sealed record ValidatedDraft(AllItemDraft Draft, IReadOnlyCollection<int> ChildrenToReparent);
}
