using FluentValidation;

namespace SIGERSA.Application.Companies;

public sealed record CompanyRequest(
    string LegalName,
    string TaxId,
    string? TradeName,
    string? EconomicActivity,
    string? Phone,
    string? Email,
    string? Address,
    Guid? MunicipalityId,
    IReadOnlyList<CompanyContactRequest> Contacts,
    string Status,
    long? RowVersion);

public sealed record CompanyContactRequest(
    string Type,
    string FullName,
    string? Identification,
    string? Phone,
    string? Email);

public sealed class CompanyRequestValidator : AbstractValidator<CompanyRequest>
{
    public CompanyRequestValidator()
    {
        RuleFor(request => request.LegalName).NotEmpty().MaximumLength(250);
        RuleFor(request => request.TaxId).NotEmpty().MaximumLength(30);
        RuleFor(request => request.TradeName).MaximumLength(250);
        RuleFor(request => request.EconomicActivity).MaximumLength(250);
        RuleFor(request => request.Phone).MaximumLength(40);
        RuleFor(request => request.Email).EmailAddress().MaximumLength(320)
            .When(request => !string.IsNullOrWhiteSpace(request.Email));
        RuleFor(request => request.Address).MaximumLength(2000);
        RuleFor(request => request.Status).Must(value => value is "ACTIVA" or "INACTIVA" or "SUSPENDIDA");
        RuleFor(request => request.Contacts).NotNull()
            .Must(contacts => contacts.Select(contact => contact.Type.Trim().ToUpperInvariant()).Distinct().Count() == contacts.Count)
            .WithMessage("Solo puede registrar un contacto de cada tipo.");
        RuleForEach(request => request.Contacts).ChildRules(contact =>
        {
            contact.RuleFor(value => value.Type)
                .Must(value => value.Trim().ToUpperInvariant() is "LEGAL" or "CALIDAD" or "PRINCIPAL");
            contact.RuleFor(value => value.FullName).NotEmpty().MaximumLength(250);
            contact.RuleFor(value => value.Identification).MaximumLength(100);
            contact.RuleFor(value => value.Phone).MaximumLength(40);
            contact.RuleFor(value => value.Email).EmailAddress().MaximumLength(320)
                .When(value => !string.IsNullOrWhiteSpace(value.Email));
        });
    }
}
