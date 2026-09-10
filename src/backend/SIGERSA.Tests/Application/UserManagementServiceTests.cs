using SIGERSA.Application.Users;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Security;
using SIGERSA.Infrastructure.Security;

namespace SIGERSA.Tests.Application;

public sealed class UserManagementServiceTests
{
    private static readonly Guid CompanyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task CoordinatorReadsUsersWithoutACompanyRestriction()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        await service.SearchAsync(null, null, null, 1, 10, Actor("COORDINADOR"), CancellationToken.None);

        Assert.Null(repository.LastQuery?.CompanyScope);
    }

    [Fact]
    public async Task CompanyAdministratorReadsOnlyItsOwnCompany()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        await service.SearchAsync(null, null, null, 1, 10, Actor("ADMINISTRADOR_EMPRESA", CompanyId), CancellationToken.None);

        Assert.Equal(CompanyId, repository.LastQuery?.CompanyScope);
    }

    [Fact]
    public async Task CompanyAdministratorCannotAssignAnInternalRole()
    {
        var service = CreateService(new FakeRepository());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(
            ValidRequest("COORDINADOR", CompanyId),
            Actor("ADMINISTRADOR_EMPRESA", CompanyId),
            CancellationToken.None));
    }

    [Fact]
    public async Task CoordinatorCannotCreateUsers()
    {
        var service = CreateService(new FakeRepository());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(
            ValidRequest("TECNICO_EVALUADOR", null),
            Actor("COORDINADOR"),
            CancellationToken.None));
    }

    [Fact]
    public async Task CompanyAdministratorReceivesOnlyItsCompanyAndEnterpriseRoles()
    {
        var service = CreateService(new FakeRepository());

        var options = await service.GetOptionsAsync(
            Actor("ADMINISTRADOR_EMPRESA", CompanyId),
            CancellationToken.None);

        Assert.True(options.CanManage);
        Assert.Equal(["ADMINISTRADOR_EMPRESA", "USUARIO_DELEGADO"], options.Roles.Select(role => role.Code));
        Assert.Collection(options.Companies, company => Assert.Equal(CompanyId, company.Id));
    }

    private static UserManagementService CreateService(FakeRepository repository) =>
        new(repository, new Pbkdf2PasswordService(), new UserManagementRequestValidator());

    private static UserManagementActor Actor(string role, Guid? companyId = null) =>
        new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), [role], companyId);

    private static UserManagementRequest ValidRequest(string role, Guid? companyId) =>
        new("Usuario de Prueba", "usuario@example.com", "CEDULA", "00100000001", null,
            companyId, role, "ACTIVO", "Temporal-2026!", null);

    private sealed class FakeRepository : IUsuarioRepository
    {
        public ManagedUsersQuery? LastQuery { get; private set; }

        public Task<ManagedUsersPage> SearchManagedAsync(ManagedUsersQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(new ManagedUsersPage([], query.Page, query.PageSize, 0));
        }

        public Task<IReadOnlyList<RoleOption>> GetActiveRoleOptionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RoleOption>>([
                new("ADMINISTRADOR", "Administrador"),
                new("ADMINISTRADOR_EMPRESA", "Administrador Empresa"),
                new("USUARIO_DELEGADO", "Usuario Delegado"),
                new("COORDINADOR", "Coordinador"),
                new("TECNICO_EVALUADOR", "Técnico Evaluador")]);

        public Task<IReadOnlyList<CompanyOption>> GetActiveCompanyOptionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CompanyOption>>([new(CompanyId, "Empresa propia"), new(Guid.NewGuid(), "Otra empresa")]);

        public Task<Guid> CreateManagedAsync(ManagedUserDraft draft, Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<bool> UpdateManagedAsync(Guid id, ManagedUserDraft draft, Guid actorId, Guid? companyScope, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> SetSuspendedAsync(Guid id, bool suspended, long versionFila, Guid actorId, Guid? companyScope, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Usuario?>(null);
        public Task ActualizarNombreAsync(Guid id, string nombreCompleto, Guid modificadoPor, long versionFila, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
