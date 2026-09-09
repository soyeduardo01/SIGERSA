using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Application.InspectionTemplates;

public sealed class AllItemsService(IAllItemsRepository repository)
{
    public Task<IReadOnlyList<AllItem>> GetAllAsync(CancellationToken cancellationToken) =>
        repository.GetAllAsync(cancellationToken);

    public async Task<AllItem> CreateAsync(AllItemDraft draft, CancellationToken cancellationToken)
    {
        await ValidateAsync(null, draft, cancellationToken);
        return await repository.CreateAsync(draft, cancellationToken);
    }

    public async Task UpdateAsync(int id, AllItemDraft draft, CancellationToken cancellationToken)
    {
        await ValidateAsync(id, draft, cancellationToken);
        if (!await repository.UpdateAsync(id, draft, cancellationToken))
        {
            throw new KeyNotFoundException("El ítem de ficha no existe.");
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var items = await repository.GetAllAsync(cancellationToken);
        var current = items.SingleOrDefault(item => item.Items == id)
            ?? throw new KeyNotFoundException("El ítem de ficha no existe.");

        if (items.Any(item => string.Equals(item.Parents, current.ItemsId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("No se puede eliminar un nodo que todavía tiene hijos.");
        }

        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            throw new KeyNotFoundException("El ítem de ficha no existe.");
        }
    }

    private async Task ValidateAsync(int? currentId, AllItemDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.ItemsId);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Description);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.SectionType);

        if (draft.Parents is null) return;
        if (string.Equals(draft.ItemsId, draft.Parents, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Un ítem no puede ser su propio padre.");
        }

        var items = await repository.GetAllAsync(cancellationToken);
        if (!items.Any(item => item.Items != currentId && item.ItemsId == draft.Parents))
        {
            throw new InvalidOperationException("El nodo padre indicado no existe.");
        }

        var parentByCode = items
            .Where(item => item.Items != currentId)
            .GroupBy(item => item.ItemsId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Parents, StringComparer.Ordinal);
        var cursor = draft.Parents;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (cursor is not null && visited.Add(cursor))
        {
            if (string.Equals(cursor, draft.ItemsId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("La jerarquía propuesta contiene un ciclo.");
            }

            parentByCode.TryGetValue(cursor, out cursor);
        }

        if (cursor is not null)
        {
            throw new InvalidOperationException("La jerarquía existente contiene un ciclo.");
        }
    }
}
