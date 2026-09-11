using SIGERSA.Application.Parameters;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Tests.Application;

public sealed class ParametersServiceTests
{
    [Fact]
    public async Task QueriesNormalizeSearchAndKeyword()
    {
        var repository = new FakeRepository();
        var service = new ParametersService(repository);

        await service.GetAllActiveAsync("  riesgo  ", CancellationToken.None);
        await service.GetActiveAsync("  NIVEL_RIESGO_ALIMENTO  ", null, CancellationToken.None);

        Assert.Equal("riesgo", repository.Search);
        Assert.Equal("NIVEL_RIESGO_ALIMENTO", repository.KeyWord);
    }

    [Fact]
    public async Task CreateRejectsCodesLongerThanTheDatabaseColumn()
    {
        var service = new ParametersService(new FakeRepository());
        var draft = new ParameterControlDraft("CATALOGO", null, 1, "1234567890", null, null, null, true, null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(draft, "admin", CancellationToken.None));
    }

    [Fact]
    public async Task MissingParameterCannotBeUpdatedOrDeleted()
    {
        var service = new ParametersService(new FakeRepository { MutationResult = false });
        var draft = new ParameterControlDraft("CATALOGO", null, 1, "A", null, null, "Activo", true, null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateAsync(1, draft, "admin", CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.SoftDeleteAsync(1, "admin", CancellationToken.None));
    }

    private sealed class FakeRepository : IParametersControlRepository
    {
        public string? Search { get; private set; }
        public string? KeyWord { get; private set; }
        public bool MutationResult { get; init; } = true;

        public Task<IReadOnlyList<ParameterControl>> GetAllActiveAsync(string? search, CancellationToken cancellationToken = default)
        {
            Search = search;
            return Task.FromResult<IReadOnlyList<ParameterControl>>([]);
        }

        public Task<IReadOnlyList<ParameterControl>> GetActiveAsync(string keyWord, int? companyCode, CancellationToken cancellationToken = default)
        {
            KeyWord = keyWord;
            return Task.FromResult<IReadOnlyList<ParameterControl>>([]);
        }

        public Task<long> CreateAsync(ParameterControlDraft parameter, string user, CancellationToken cancellationToken = default) =>
            Task.FromResult(1L);

        public Task<bool> UpdateAsync(long parametersId, ParameterControlDraft parameter, string user, CancellationToken cancellationToken = default) =>
            Task.FromResult(MutationResult);

        public Task<bool> SoftDeleteAsync(long parametersId, string user, CancellationToken cancellationToken = default) =>
            Task.FromResult(MutationResult);
    }
}
