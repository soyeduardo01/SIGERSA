using FluentValidation;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Application.Companies;

public sealed class CompanyService(ICompanyRepository repository, IValidator<CompanyRequest> validator)
{
    public Task<CompaniesPage> SearchAsync(
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        repository.SearchAsync(new CompanySearch(
            Optional(search)?.ToLowerInvariant(),
            Optional(status)?.ToUpperInvariant(),
            Math.Max(page, 1),
            Math.Clamp(pageSize, 5, 100)), cancellationToken);

    public async Task<Guid> CreateAsync(CompanyRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await repository.CreateAsync(ToDraft(request), actorId, cancellationToken);
    }

    public async Task UpdateAsync(Guid id, CompanyRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        if (request.RowVersion is null or <= 0)
            throw new ArgumentException("La versión de la empresa es obligatoria para actualizar.");
        if (!await repository.UpdateAsync(id, ToDraft(request), actorId, cancellationToken))
            throw new OptimisticConcurrencyException(id);
    }

    private static CompanyDraft ToDraft(CompanyRequest request) => new(
        request.LegalName.Trim(),
        NormalizeTaxId(request.TaxId),
        Optional(request.TradeName),
        Optional(request.EconomicActivity),
        Optional(request.Phone),
        Optional(request.Email)?.ToLowerInvariant(),
        Optional(request.Address),
        request.MunicipalityId,
        request.Contacts.Select(contact => new CompanyContact(
            contact.Type.Trim().ToUpperInvariant(),
            contact.FullName.Trim(),
            Optional(contact.Identification),
            Optional(contact.Phone),
            Optional(contact.Email)?.ToLowerInvariant(),
            Optional(contact.IdentificationType)?.ToUpperInvariant())).ToArray(),
        request.Status.Trim().ToUpperInvariant(),
        request.RowVersion);

    private static string NormalizeTaxId(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
