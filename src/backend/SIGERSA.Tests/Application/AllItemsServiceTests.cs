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
    public async Task UpdateCodeShouldRepointDirectChildren()
    {
        var repository = new InMemoryAllItemsRepository(
        [
            new AllItem(1, "1", "Raíz", "C", null),
            new AllItem(2, "1.1", "Sección", "S", "1")
        ]);
        var service = new AllItemsService(repository);

        await service.UpdateAsync(1, new AllItemDraft("A", "Raíz", "C", null), CancellationToken.None);

        Assert.Equal([2], repository.LastUpdateChildren);
    }

    [Fact]
    public async Task CreateShouldRejectAnIncompatibleSectionType()
    {
        var service = new AllItemsService(new InMemoryAllItemsRepository(
            [new AllItem(1, "1", "Raíz", "C", null)]));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new AllItemDraft("1.1.1", "Subsección", "SS", "1"), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteShouldRequireAChildStrategyForAParent()
    {
        var service = new AllItemsService(new InMemoryAllItemsRepository(
        [
            new AllItem(1, "1", "Raíz", "C", null),
            new AllItem(2, "1.1", "Hijo", "I", "1")
        ]));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteAsync(1, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteSubtreeShouldIncludeEveryDescendant()
    {
        var repository = new InMemoryAllItemsRepository(
        [
            new AllItem(1, "1", "Raíz", "C", null),
            new AllItem(2, "1.1", "Sección", "S", "1"),
            new AllItem(3, "1.1.1", "Pregunta", "I", "1.1")
        ]);
        var service = new AllItemsService(repository);

        await service.DeleteAsync(1, AllItemsService.DeleteSubtree, null, CancellationToken.None);

        Assert.Equal([1, 2, 3], repository.LastDeletedItems?.Order());
        Assert.Empty(repository.LastReparenting!);
    }

    [Fact]
    public async Task DeleteShouldReparentChildrenToTheDeletedNodesParent()
    {
        var repository = new InMemoryAllItemsRepository(
        [
            new AllItem(1, "1", "Raíz", "C", null),
            new AllItem(2, "1.1", "Sección", "S", "1"),
            new AllItem(3, "1.1.1", "Pregunta", "I", "1.1")
        ]);
        var service = new AllItemsService(repository);

        await service.DeleteAsync(2, AllItemsService.ReparentChildren, null, CancellationToken.None);

        Assert.Equal([2], repository.LastDeletedItems);
        Assert.Equal("1", repository.LastReparenting![3]);
    }

    [Fact]
    public async Task ReorderShouldRejectAChildBeforeItsParent()
    {
        var service = new AllItemsService(new InMemoryAllItemsRepository(
        [
            new AllItem(1, "1", "Raíz", "C", null),
            new AllItem(2, "1.1", "Sección", "S", "1")
        ]));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReorderAsync([2, 1], CancellationToken.None));
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
        public IReadOnlyCollection<int>? LastDeletedItems { get; private set; }
        public IReadOnlyDictionary<int, string?>? LastReparenting { get; private set; }
        public IReadOnlyCollection<int>? LastUpdateChildren { get; private set; }
        public Task<IReadOnlyList<AllItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AllItem>>(_items);
        public Task<AllItem?> GetByIdAsync(int items, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.SingleOrDefault(item => item.Items == items));
        public Task<AllItem> CreateAsync(AllItemDraft item, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AllItem(_items.Count + 1, item.ItemsId, item.Description, item.SectionType, item.Parents));
        public Task<bool> UpdateAsync(int items, AllItemDraft item, IReadOnlyCollection<int> childrenToReparent, CancellationToken cancellationToken = default)
        {
            LastUpdateChildren = childrenToReparent;
            return Task.FromResult(true);
        }
        public Task<int> ApplyDeleteAsync(IReadOnlyCollection<int> itemsToDelete, IReadOnlyDictionary<int, string?> childrenToReparent, CancellationToken cancellationToken = default)
        {
            LastDeletedItems = itemsToDelete;
            LastReparenting = childrenToReparent;
            return Task.FromResult(itemsToDelete.Count);
        }
        public Task<IReadOnlyList<AllItem>> ReorderAsync(IReadOnlyList<int> orderedItems, CancellationToken cancellationToken = default)
        {
            LastOrder = orderedItems;
            return Task.FromResult<IReadOnlyList<AllItem>>(_items);
        }
    }
}
