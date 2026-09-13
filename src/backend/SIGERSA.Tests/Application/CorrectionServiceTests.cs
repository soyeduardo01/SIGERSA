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
    public async Task CompanyActorCannotReadTechnicalCorrectionWorkflow()
    {
        var service = CreateService(new FakeRepository());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.SearchAsync(null, null, 1, 10,
            new CorrectionActor(UserId, ["ADMINISTRADOR_EMPRESA"], CompanyId), CancellationToken.None));
    }

    [Fact]
    public async Task TechnicianReaderIsRestrictedToAssignments()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        await service.SearchAsync(null, null, 1, 10,
            new CorrectionActor(UserId, ["TECNICO_EVALUADOR"], null), CancellationToken.None);

        Assert.True(repository.LastSearch!.AssignedOnly);
        Assert.Null(repository.LastSearch.ReviewScope);
    }

    [Theory]
    [InlineData("COORDINADOR", "COORDINADOR")]
    [InlineData("ADMINISTRADOR", "ADMINISTRADOR")]
    public async Task ReviewerOnlyReceivesItsWorkflowStage(string role, string expectedScope)
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        await service.SearchAsync(null, null, 1, 10,
            new CorrectionActor(UserId, [role], null), CancellationToken.None);

        Assert.Equal(expectedScope, repository.LastSearch?.ReviewScope);
    }

    [Fact]
    public async Task CompanyActorCannotRequestCorrection()
    {
        var service = CreateService(new FakeRepository());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(
            ValidInput(), new CorrectionActor(UserId, ["ADMINISTRADOR_EMPRESA"], CompanyId), CancellationToken.None));
    }

    [Fact]
    public async Task TechnicianRequestIsAssignedToItselfAndSentToCoordinator()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        await service.CreateAsync(ValidInput(),
            new CorrectionActor(UserId, ["TECNICO_EVALUADOR"], null), CancellationToken.None);

        Assert.Equal("TECNICO", repository.LastDraft?.ResponsibleType);
        Assert.Equal(UserId, repository.LastDraft?.AssignedToId);
    }

    [Fact]
    public async Task CoordinatorCannotRequestCorrection()
    {
        var service = CreateService(new FakeRepository());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(
            ValidInput(), new CorrectionActor(UserId, ["COORDINADOR"], null), CancellationToken.None));
    }

    [Fact]
    public async Task CoordinatorCanSendApprovedCorrectionToAdministrator()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        await service.ResolveAsync(
            Guid.NewGuid(), new CorrectionTransitionInput(1), "ACEPTADA",
            new CorrectionActor(UserId, ["COORDINADOR"], null), CancellationToken.None);

        Assert.True(repository.LastCoordinatorReview);
    }

    [Fact]
    public async Task AdministratorPerformsFinalCorrectionDecision()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        await service.ResolveAsync(
            Guid.NewGuid(), new CorrectionTransitionInput(1), "ACEPTADA",
            new CorrectionActor(UserId, ["ADMINISTRADOR"], null), CancellationToken.None);

        Assert.False(repository.LastCoordinatorReview);
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
        public CorrectionDraft? LastDraft { get; private set; }
        public Task<CorrectionsPage> SearchAsync(CorrectionSearch query, CancellationToken cancellationToken = default)
        {
            LastSearch = query;
            return Task.FromResult(new CorrectionsPage([], query.Page, query.PageSize, 0));
        }
        public Task<CorrectionOptions> GetOptionsAsync(Guid actorId, bool canCreate, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CorrectionOptions([], [], canCreate));
        public Task<Guid> CreateAsync(CorrectionDraft draft, Guid actorId, CancellationToken cancellationToken = default)
        {
            LastDraft = draft;
            return Task.FromResult(Guid.NewGuid());
        }
        public Task<bool> SubmitAsync(Guid id, long rowVersion, Guid actorId, Guid? companyScope, bool globalScope, bool assignedOnly, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public bool LastCoordinatorReview { get; private set; }
        public Task<bool> ResolveAsync(Guid id, long rowVersion, string targetStatus, Guid actorId,
            bool coordinatorReview, CancellationToken cancellationToken = default)
        {
            LastCoordinatorReview = coordinatorReview;
            return Task.FromResult(true);
        }
    }
}
