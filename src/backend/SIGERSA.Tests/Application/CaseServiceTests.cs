using SIGERSA.Application.Cases;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Tests.Application;

public sealed class CaseServiceTests
{
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CompanyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task CompanyReaderIsRestrictedToTokenCompany()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        await service.SearchAsync(null, null, 1, 10,
            new CaseActor(UserId, ["USUARIO_DELEGADO"], CompanyId), CancellationToken.None);

        Assert.Equal(CompanyId, repository.LastSearch?.CompanyScope);
        Assert.False(repository.LastSearch!.GlobalScope);
        Assert.False(repository.LastSearch.AssignedOnly);
    }

    [Fact]
    public async Task TechnicianReaderIsRestrictedToAssignments()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        await service.SearchAsync(null, null, 1, 10,
            new CaseActor(UserId, ["TECNICO_EVALUADOR"], null), CancellationToken.None);

        Assert.True(repository.LastSearch!.AssignedOnly);
        Assert.Null(repository.LastSearch.CompanyScope);
    }

    [Fact]
    public async Task CompanyActorCannotManageCases()
    {
        var service = CreateService(new FakeRepository());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(
            ValidInput(), new CaseActor(UserId, ["ADMINISTRADOR_EMPRESA"], CompanyId), CancellationToken.None));
    }

    [Fact]
    public async Task MissingRowVersionProducesConcurrencyConflict()
    {
        var service = CreateService(new FakeRepository { UpdateResult = false });
        var input = ValidInput() with { RowVersion = 2 };

        await Assert.ThrowsAsync<OptimisticConcurrencyException>(() => service.UpdateAsync(
            Guid.NewGuid(), input, new CaseActor(UserId, ["COORDINADOR"], null), CancellationToken.None));
    }

    private static CaseService CreateService(FakeRepository repository) =>
        new(repository, new CaseInputValidator());

    private static CaseInput ValidInput() =>
        new("SOLICITUD_EMPRESA", Guid.NewGuid(), 2, null, "PROCEDE", "Cumple los criterios", Guid.NewGuid(), null);

    private sealed class FakeRepository : ICaseRepository
    {
        public CaseSearch? LastSearch { get; private set; }
        public bool UpdateResult { get; init; } = true;

        public Task<CasesPage> SearchAsync(CaseSearch query, CancellationToken cancellationToken = default)
        {
            LastSearch = query;
            return Task.FromResult(new CasesPage([], query.Page, query.PageSize, 0));
        }

        public Task<CaseOptions> GetOptionsAsync(bool canManage, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CaseOptions([], [], canManage));

        public Task<Guid> CreateAsync(CaseDraft draft, Guid actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Guid.NewGuid());

        public Task<bool> UpdateAsync(Guid id, CaseDraft draft, Guid actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(UpdateResult);

        public Task<bool> CloseAsync(Guid id, long rowVersion, string reason, Guid actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
