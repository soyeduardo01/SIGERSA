using SIGERSA.Domain.Entities;

namespace SIGERSA.Domain.Repositories;

public interface IParametersControlRepository
{
    Task<IReadOnlyList<ParameterControl>> GetAllActiveAsync(
        string? search,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParameterControl>> GetActiveAsync(
        string keyWord,
        int? companyCode,
        CancellationToken cancellationToken = default);

    Task<long> CreateAsync(
        ParameterControlDraft parameter,
        string user,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        long parametersId,
        ParameterControlDraft parameter,
        string user,
        CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteAsync(
        long parametersId,
        string user,
        CancellationToken cancellationToken = default);
}
