using FluentValidation;
using SIGERSA.Domain.Entities;

namespace SIGERSA.Application.Establishments;

public sealed record EstablishmentRequest(
    Guid CompanyId,
    Guid? MunicipalityId,
    Guid? DpsDasId,
    Guid? CommercializationId,
    string Code,
    string Name,
    string? Street,
    string? AddressNumber,
    string? Phone,
    string? Email,
    DateTimeOffset? OperationsStartDate,
    string? SanitaryPermitNumber,
    DateTimeOffset? SanitaryPermitExpiresAt,
    string? ProductsDescription,
    decimal? AnnualProduction,
    int? FemaleEmployees,
    int? MaleEmployees,
    int MicrobiologicalRejectionsLastFiveYears,
    bool? HaccpImplemented,
    decimal? HaccpPercentage,
    bool? MicrobiologicalSamplingPlan,
    string? SamplingApplicationCode,
    bool? IsInabieSupplier,
    string? InabieDistributionCode,
    string Status,
    IReadOnlyList<Guid>? MarketIds,
    IReadOnlyList<EstablishmentContactDraft>? Contacts,
    IReadOnlyList<EstablishmentProductDraft>? Products,
    long? RowVersion);

public sealed class EstablishmentRequestValidator : AbstractValidator<EstablishmentRequest>
{
    public EstablishmentRequestValidator()
    {
        RuleFor(request => request.CompanyId).NotEmpty();
        RuleFor(request => request.Code).NotEmpty().MaximumLength(50);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(250);
        RuleFor(request => request.Street).MaximumLength(250);
        RuleFor(request => request.AddressNumber).MaximumLength(50);
        RuleFor(request => request.Phone).MaximumLength(40);
        RuleFor(request => request.Email).EmailAddress().MaximumLength(320)
            .When(request => !string.IsNullOrWhiteSpace(request.Email));
        RuleFor(request => request.SanitaryPermitNumber).MaximumLength(100);
        RuleFor(request => request.AnnualProduction).GreaterThanOrEqualTo(0).When(request => request.AnnualProduction.HasValue);
        RuleFor(request => request.FemaleEmployees).GreaterThanOrEqualTo(0).When(request => request.FemaleEmployees.HasValue);
        RuleFor(request => request.MaleEmployees).GreaterThanOrEqualTo(0).When(request => request.MaleEmployees.HasValue);
        RuleFor(request => request.MicrobiologicalRejectionsLastFiveYears).GreaterThanOrEqualTo(0);
        RuleFor(request => request.Status).Must(value => value is "ACTIVO" or "INACTIVO" or "SUSPENDIDO");
        RuleFor(request => request.HaccpPercentage)
            .Must(value => value is 25 or 75 or 100)
            .When(request => request.HaccpImplemented == true)
            .WithMessage("Seleccione el nivel de implementación HACCP.");
        RuleFor(request => request.HaccpPercentage).Null().When(request => request.HaccpImplemented != true);
        RuleFor(request => request.SamplingApplicationCode).NotEmpty().When(request => request.MicrobiologicalSamplingPlan == true);
        RuleFor(request => request.SamplingApplicationCode).Null().When(request => request.MicrobiologicalSamplingPlan != true);
        RuleFor(request => request.InabieDistributionCode).NotEmpty().When(request => request.IsInabieSupplier == true);
        RuleFor(request => request.InabieDistributionCode).Null().When(request => request.IsInabieSupplier != true);
        RuleForEach(request => request.Contacts).ChildRules(contact =>
        {
            contact.RuleFor(value => value.Type).Must(value => value is "PRINCIPAL" or "LEGAL");
            contact.RuleFor(value => value.FullName).NotEmpty().MaximumLength(250);
            contact.RuleFor(value => value.Identification).MaximumLength(100);
            contact.RuleFor(value => value.Phone).MaximumLength(40);
            contact.RuleFor(value => value.Email).EmailAddress().MaximumLength(320)
                .When(value => !string.IsNullOrWhiteSpace(value.Email));
        });
        RuleForEach(request => request.Products).ChildRules(product =>
        {
            product.RuleFor(value => value.SubcategoryId).NotEmpty();
            product.RuleFor(value => value.Description).NotEmpty().MaximumLength(300);
            product.RuleFor(value => value.MonthlyVolume).GreaterThanOrEqualTo(0).When(value => value.MonthlyVolume.HasValue);
            product.RuleFor(value => value.Unit).MaximumLength(30);
        });
    }
}
