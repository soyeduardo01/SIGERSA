using FluentValidation;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Application.Establishments;

public sealed class EstablishmentService(
    IEstablishmentRepository repository,
    IValidator<EstablishmentRequest> validator)
{
    public Task<EstablishmentsPage> SearchAsync(string? search, string? status, int page, int pageSize, CancellationToken cancellationToken) =>
        repository.SearchAsync(new EstablishmentSearch(
            NormalizeOptional(search)?.ToLowerInvariant(),
            NormalizeOptional(status)?.ToUpperInvariant(),
            Math.Max(page, 1),
            Math.Clamp(pageSize, 5, 100)), cancellationToken);

    public async Task<EstablishmentDetails> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("El establecimiento no existe.");

    public Task<EstablishmentOptions> GetOptionsAsync(CancellationToken cancellationToken) =>
        repository.GetOptionsAsync(cancellationToken);

    public async Task<Guid> CreateAsync(EstablishmentRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return await repository.CreateAsync(ToDraft(request), actorId, cancellationToken);
    }

    public async Task UpdateAsync(Guid id, EstablishmentRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        if (request.RowVersion is null or <= 0)
            throw new ArgumentException("La versión del establecimiento es obligatoria para actualizar.");
        if (!await repository.UpdateAsync(id, ToDraft(request), actorId, cancellationToken))
            throw new OptimisticConcurrencyException(id);
    }

    private static EstablishmentDraft ToDraft(EstablishmentRequest request) => new(
        request.CompanyId, request.MunicipalityId, request.DpsDasId, request.CommercializationId,
        string.Empty, request.Name.Trim(), NormalizeOptional(request.Street),
        NormalizeOptional(request.AddressNumber), NormalizeOptional(request.Phone),
        NormalizeOptional(request.Email)?.ToLowerInvariant(), NormalizeUtc(request.OperationsStartDate),
        NormalizeOptional(request.SanitaryPermitNumber), NormalizeUtc(request.SanitaryPermitExpiresAt),
        NormalizeOptional(request.ProductsDescription), request.AnnualProduction, request.FemaleEmployees,
        request.MaleEmployees, request.MicrobiologicalRejectionsLastFiveYears,
        request.HaccpImplemented, request.HaccpImplemented == true ? request.HaccpPercentage : null,
        request.MicrobiologicalSamplingPlan,
        request.MicrobiologicalSamplingPlan == true ? NormalizeOptional(request.SamplingApplicationCode) : null,
        request.IsInabieSupplier,
        request.IsInabieSupplier == true ? NormalizeOptional(request.InabieDistributionCode) : null,
        request.Status.Trim().ToUpperInvariant(), request.MarketIds ?? [], NormalizeContacts(request.Contacts),
        request.Products ?? [], request.RowVersion);

    private static EstablishmentContactDraft[] NormalizeContacts(
        IReadOnlyList<EstablishmentContactDraft>? contacts) =>
        (contacts ?? []).Select(contact => new EstablishmentContactDraft(
            contact.Type.Trim().ToUpperInvariant(),
            contact.FullName.Trim(),
            NormalizeIdentification(contact.Identification),
            NormalizeOptional(contact.Phone),
            NormalizeOptional(contact.Email)?.ToLowerInvariant())).ToArray();

    private static string? NormalizeIdentification(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    }

    private static DateTimeOffset? NormalizeUtc(DateTimeOffset? value) => value?.ToUniversalTime();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
