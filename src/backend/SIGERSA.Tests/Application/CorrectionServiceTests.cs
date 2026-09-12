using SIGERSA.Application.Corrections;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Tests.Application;

public sealed class CorrectionServiceTests
{
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CompanyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task CompanyReaderIsRestrictedToItsCompany()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        await service.SearchAsync(null, null, 1, 10,
            new CorrectionActor(UserId, ["ADMINISTRADOR_EMPRESA"], CompanyId), CancellationToken.None);

        Assert.Equal(CompanyId, repository.LastSearch?.CompanyScope);
        Assert.False(repository.LastSearch!.GlobalScope);
    }

    [Fact]
    public async Task TechnicianReaderIsRestrictedToAssignments()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        await service.SearchAsync(null, null, 1, 10,
            new CorrectionActor(UserId, ["TECNICO_EVALUADOR"], null), CancellationToken.None);

        Assert.True(repository.LastSearch!.AssignedOnly);
    }

    [Fact]
    public async Task CompanyActorCannotRequestCorrection()
    {
        var service = CreateService(new FakeRepository());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(
            ValidInput(), new CorrectionActor(UserId, ["ADMINISTRADOR_EMPRESA"], CompanyId), CancellationToken.None));
    }

    [Fact]
    public async Task CoordinatorCannotSubmitAResponseOnBehalfOfResponsibleParty()
    {
        var service = CreateService(new FakeRepository());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.SubmitAsync(
            Guid.NewGuid(), new CorrectionTransitionInput(1),
            new CorrectionActor(UserId, ["COORDINADOR"], null), CancellationToken.None));
    }

    [Fact]
    public void DueDateUsesUtcInstantAndRequiresTheMinimumResolutionTime()
    {
        var validator = new CorrectionInputValidator();

        var tooSoon = ValidInput() with { DueAt = DateTimeOffset.Now.AddMinutes(5) };
        var valid = ValidInput() with { DueAt = DateTimeOffset.Now.AddHours(1) };

        Assert.False(validator.Validate(tooSoon).IsValid);
        Assert.True(validator.Validate(valid).IsValid);
    }

    private static CorrectionService CreateService(FakeRepository repository) =>
        new(repository, new CorrectionInputValidator());

    private static CorrectionInput ValidInput() =>
        new(Guid.NewGuid(), "EMPRESA", null, "Corregir información", DateTimeOffset.UtcNow.AddDays(2), Guid.NewGuid());

    private sealed class FakeRepository : ICorrectionRepository
    {
        public CorrectionSearch? LastSearch { get; private set; }
        public Task<CorrectionsPage> SearchAsync(CorrectionSearch query, CancellationToken cancellationToken = default)
        {
            LastSearch = query;
            return Task.FromResult(new CorrectionsPage([], query.Page, query.PageSize, 0));
        }
        public Task<CorrectionOptions> GetOptionsAsync(bool canCreate, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CorrectionOptions([], [], canCreate));
        public Task<Guid> CreateAsync(CorrectionDraft draft, Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<bool> SubmitAsync(Guid id, long rowVersion, Guid actorId, Guid? companyScope, bool globalScope, bool assignedOnly, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> ResolveAsync(Guid id, long rowVersion, string targetStatus, Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
