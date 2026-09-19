using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Application.Parameters;

public sealed class ParametersService(IParametersControlRepository repository)
{
    public Task<IReadOnlyList<ParameterControl>> GetAllAsync(
        string? search,
        CancellationToken cancellationToken) =>
        repository.GetAllAsync(string.IsNullOrWhiteSpace(search) ? null : search.Trim(), cancellationToken);

    public Task<IReadOnlyList<ParameterControl>> GetOfflineSnapshotAsync(
        CancellationToken cancellationToken) =>
        repository.GetAllAsync(null, cancellationToken);

    public Task<IReadOnlyList<ParameterControl>> GetActiveAsync(
        string keyWord,
        int? companyCode,
        CancellationToken cancellationToken) =>
        repository.GetActiveAsync(Required(keyWord, nameof(keyWord)), companyCode, cancellationToken);

    public Task<long> CreateAsync(ParameterControlDraft draft, string user, CancellationToken cancellationToken) =>
        repository.CreateAsync(Validate(draft), Required(user, nameof(user)), cancellationToken);

    public async Task UpdateAsync(long id, ParameterControlDraft draft, string user, CancellationToken cancellationToken)
    {
        if (!await repository.UpdateAsync(id, Validate(draft), Required(user, nameof(user)), cancellationToken))
        {
            throw new KeyNotFoundException("El parámetro no existe o está inactivo.");
        }
    }

    public async Task SoftDeleteAsync(long id, string user, CancellationToken cancellationToken)
    {
        if (!await repository.SoftDeleteAsync(id, Required(user, nameof(user)), cancellationToken))
        {
            throw new KeyNotFoundException("El parámetro no existe o ya está inactivo.");
        }
    }

    public async Task ActivateAsync(long id, string user, CancellationToken cancellationToken)
    {
        if (!await repository.ActivateAsync(id, Required(user, nameof(user)), cancellationToken))
        {
            throw new KeyNotFoundException("El parámetro no existe o ya está activo.");
        }
    }

    private static ParameterControlDraft Validate(ParameterControlDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        Required(draft.KeyWord, nameof(draft.KeyWord));
        if (draft.CCode?.Length > 9) throw new ArgumentException("CCode no puede exceder 9 caracteres.", nameof(draft));
        return draft;
    }

    private static string Required(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value.Trim();
    }
}
