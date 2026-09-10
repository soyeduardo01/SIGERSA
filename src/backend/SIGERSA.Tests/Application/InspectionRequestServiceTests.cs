using SIGERSA.Application.Requests;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Tests.Application;

public sealed class InspectionRequestServiceTests
{
    private static readonly Guid CompanyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task CompanyActorSearchIsRestrictedToItsCompany()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        await service.SearchAsync(null, null, 1, 10, Actor("USUARIO_DELEGADO", CompanyId), CancellationToken.None);

        Assert.Equal(CompanyId, repository.LastSearch?.CompanyScope);
        Assert.False(repository.LastSearch!.GlobalScope);
        Assert.False(repository.LastSearch.AssignedOnly);
    }

    [Fact]
    public async Task TechnicianSearchIsRestrictedToAssignments()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        await service.SearchAsync(null, null, 1, 10, Actor("TECNICO_EVALUADOR"), CancellationToken.None);

        Assert.True(repository.LastSearch!.AssignedOnly);
        Assert.False(repository.LastSearch.GlobalScope);
    }

    [Fact]
    public async Task CompanyActorCannotCreateForAnotherCompany()
    {
        var service = CreateService(new FakeRepository());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(
            ValidInput(Guid.NewGuid()), Actor("ADMINISTRADOR_EMPRESA", CompanyId), CancellationToken.None));
    }

    [Fact]
    public async Task TechnicianCannotModifyRequests()
    {
        var service = CreateService(new FakeRepository());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.TransitionAsync(
            Guid.NewGuid(), new InspectionRequestTransitionInput(1), "CANCELADA",
            Actor("TECNICO_EVALUADOR"), CancellationToken.None));
    }

    [Fact]
    public async Task CompanyActorUsesTokenCompanyInsteadOfClientInput()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        await service.CreateAsync(ValidInput(null), Actor("USUARIO_DELEGADO", CompanyId), CancellationToken.None);

        Assert.Equal(CompanyId, repository.LastDraft?.CompanyId);
        Assert.Equal(UserId, repository.LastApplicantId);
    }

    private static InspectionRequestService CreateService(FakeRepository repository) =>
        new(repository, new InspectionRequestInputValidator());

    private static InspectionRequestActor Actor(string role, Guid? companyId = null) =>
        new(UserId, [role], companyId);

    private static InspectionRequestInput ValidInput(Guid? companyId) =>
        new(companyId, null, Guid.NewGuid(), "Detalle", null, null, Guid.NewGuid(), null);

    private sealed class FakeRepository : IInspectionRequestRepository
    {
        public InspectionRequestSearch? LastSearch { get; private set; }
        public InspectionRequestDraft? LastDraft { get; private set; }
        public Guid? LastApplicantId { get; private set; }

        public Task<InspectionRequestsPage> SearchAsync(
            InspectionRequestSearch query,
            CancellationToken cancellationToken = default)
        {
            LastSearch = query;
            return Task.FromResult(new InspectionRequestsPage([], query.Page, query.PageSize, 0));
        }

        public Task<InspectionRequestOptions> GetOptionsAsync(
            Guid? companyScope,
            bool canManage,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new InspectionRequestOptions([], [], [], canManage));

        public Task<Guid> CreateAsync(
            InspectionRequestDraft draft,
            Guid applicantId,
            CancellationToken cancellationToken = default)
        {
            LastDraft = draft;
            LastApplicantId = applicantId;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<bool> UpdateAsync(
            Guid id,
            InspectionRequestDraft draft,
            Guid actorId,
            Guid? companyScope,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> TransitionAsync(
            Guid id,
            long rowVersion,
            string targetStatus,
            Guid actorId,
            Guid? companyScope,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
