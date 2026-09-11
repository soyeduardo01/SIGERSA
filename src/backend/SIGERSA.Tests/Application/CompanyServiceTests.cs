using SIGERSA.Application.Companies;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Tests.Application;

public sealed class CompanyServiceTests
{
    [Fact]
    public async Task CreateNormalizesRncAndContactData()
    {
        var repository = new FakeRepository();
        var service = new CompanyService(repository, new CompanyRequestValidator());

        await service.CreateAsync(new CompanyRequest(
            "  Industria Alimentaria  ", "1-01-12345-6", "  Marca  ", null,
            " 809-555-0101 ", " INFO@EXAMPLE.COM ", null, null, [], "ACTIVA", null),
            Guid.NewGuid(), CancellationToken.None);

        Assert.NotNull(repository.LastDraft);
        Assert.Equal("101123456", repository.LastDraft.TaxId);
        Assert.Equal("Industria Alimentaria", repository.LastDraft.LegalName);
        Assert.Equal("info@example.com", repository.LastDraft.Email);
    }

    [Fact]
    public async Task UpdateRequiresOptimisticConcurrencyVersion()
    {
        var service = new CompanyService(new FakeRepository(), new CompanyRequestValidator());
        var request = new CompanyRequest("Empresa", "101000001", null, null, null, null,
            null, null, [], "ACTIVA", null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(Guid.NewGuid(), request, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateReportsVersionConflict()
    {
        var service = new CompanyService(
            new FakeRepository { AllowUpdate = false }, new CompanyRequestValidator());
        var request = new CompanyRequest("Empresa", "101000001", null, null, null, null,
            null, null, [], "ACTIVA", 1);

        await Assert.ThrowsAsync<OptimisticConcurrencyException>(() =>
            service.UpdateAsync(Guid.NewGuid(), request, Guid.NewGuid(), CancellationToken.None));
    }

    private sealed class FakeRepository : ICompanyRepository
    {
        public CompanyDraft? LastDraft { get; private set; }
        public bool AllowUpdate { get; init; } = true;

        public Task<CompaniesPage> SearchAsync(CompanySearch search, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CompaniesPage([], search.Page, search.PageSize, 0));

        public Task<Guid> CreateAsync(CompanyDraft draft, Guid actorId, CancellationToken cancellationToken = default)
        {
            LastDraft = draft;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<bool> UpdateAsync(Guid id, CompanyDraft draft, Guid actorId, CancellationToken cancellationToken = default)
        {
            LastDraft = draft;
            return Task.FromResult(AllowUpdate);
        }
    }
}
