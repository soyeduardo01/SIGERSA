using SIGERSA.Application.Risk;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Tests.Application;

public sealed class InspectionRiskServiceTests
{
    private static readonly AllItem[] Items =
    [
        new(1, "1", "Categoría", "C", null),
        new(2, "1.1", "Sección", "S", "1"),
        new(3, "1.1.1", "Pregunta uno", "I", "1.1"),
        new(4, "1.1.2", "Pregunta dos", "I", "1.1")
    ];

    [Fact]
    public async Task CalculateAsyncShouldMapPublicCodesAndAggregateTheTree()
    {
        var service = new InspectionRiskService(new FakeRepository(Items));

        var result = await service.CalculateAsync(
            [new(3, "CUMPLE"), new(4, "NO_CUMPLE")],
            CancellationToken.None);

        Assert.Equal(0.5m, result.Single(score => score.Item == 1).Score);
    }

    [Fact]
    public async Task CalculateAsyncShouldRejectRatingsForGroupingNodes()
    {
        var service = new InspectionRiskService(new FakeRepository(Items));

        await Assert.ThrowsAsync<ArgumentException>(() => service.CalculateAsync(
            [new(1, "CUMPLE")],
            CancellationToken.None));
    }

    private sealed class FakeRepository(IReadOnlyList<AllItem> items) : IAllItemsRepository
    {
        public Task<IReadOnlyList<AllItem>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(items);
        public Task<AllItem?> GetByIdAsync(int item, CancellationToken cancellationToken = default) => Task.FromResult(items.SingleOrDefault(value => value.Items == item));
        public Task<AllItem> CreateAsync(AllItemDraft item, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(int item, AllItemDraft value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(int item, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
