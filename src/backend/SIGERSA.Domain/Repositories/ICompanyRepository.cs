using SIGERSA.Domain.Entities;

namespace SIGERSA.Domain.Repositories;

public interface ICompanyRepository
{
    Task<CompaniesPage> SearchAsync(CompanySearch search, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CompanyDraft draft, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Guid id, CompanyDraft draft, Guid actorId, CancellationToken cancellationToken = default);
}
