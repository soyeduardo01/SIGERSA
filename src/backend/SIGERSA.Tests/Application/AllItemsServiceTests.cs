using SIGERSA.Application.InspectionTemplates;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Tests.Application;

public sealed class AllItemsServiceTests
{
    [Fact]
    public async Task CreateShouldRejectAMissingParent()
    {
        var service = new AllItemsService(new InMemoryAllItemsRepository(
            [new AllItem(1, "1", "Raíz", "C", null)]));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new AllItemDraft("2.1", "Ítem", "I", "2"), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateShouldRejectACycle()
    {
        var service = new AllItemsService(new InMemoryAllItemsRepository(
        [
            new AllItem(1, "1", "Raíz", "C", null),
            new AllItem(2, "1.1", "Hijo", "S", "1")
        ]));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(1, new AllItemDraft("1", "Raíz", "C", "1.1"), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteShouldRejectAParentWithChildren()
    {
        var service = new AllItemsService(new InMemoryAllItemsRepository(
        [
            new AllItem(1, "1", "Raíz", "C", null),
            new AllItem(2, "1.1", "Hijo", "I", "1")
        ]));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task ReorderShouldDelegateACompleteOrder()
    {
        var repository = new InMemoryAllItemsRepository(
        [
            new AllItem(1, "1", "Primero", "C", null),
            new AllItem(2, "2", "Segundo", "C", null)
        ]);
        var service = new AllItemsService(repository);

        await service.ReorderAsync([2, 1], CancellationToken.None);

        Assert.Equal([2, 1], repository.LastOrder);
    }

    private sealed class InMemoryAllItemsRepository(IEnumerable<AllItem> seed) : IAllItemsRepository
    {
        private readonly List<AllItem> _items = [.. seed];
        public IReadOnlyList<int>? LastOrder { get; private set; }
        public Task<IReadOnlyList<AllItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AllItem>>(_items);
        public Task<AllItem?> GetByIdAsync(int items, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.SingleOrDefault(item => item.Items == items));
        public Task<AllItem> CreateAsync(AllItemDraft item, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AllItem(_items.Count + 1, item.ItemsId, item.Description, item.SectionType, item.Parents));
        public Task<bool> UpdateAsync(int items, AllItemDraft item, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> DeleteAsync(int items, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<IReadOnlyList<AllItem>> ReorderAsync(IReadOnlyList<int> orderedItems, CancellationToken cancellationToken = default)
        {
            LastOrder = orderedItems;
            return Task.FromResult<IReadOnlyList<AllItem>>(_items);
        }
    }
}
