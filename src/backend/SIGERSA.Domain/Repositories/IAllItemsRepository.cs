using SIGERSA.Domain.Entities;

namespace SIGERSA.Domain.Repositories;

public interface IAllItemsRepository
{
    Task<IReadOnlyList<AllItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<AllItem?> GetByIdAsync(int items, CancellationToken cancellationToken = default);

    Task<AllItem> CreateAsync(AllItemDraft item, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(int items, AllItemDraft item, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int items, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AllItem>> ReorderAsync(
        IReadOnlyList<int> orderedItems,
        CancellationToken cancellationToken = default);
}
