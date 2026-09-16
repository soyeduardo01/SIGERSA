namespace SIGERSA.Domain.Entities;

public sealed record CompanySummary(
    Guid Id,
    string LegalName,
    string TaxId,
    string? TradeName,
    string? EconomicActivity,
    string? Phone,
    string? Email,
    string? Address,
    Guid? MunicipalityId,
    string? MunicipalityName,
    string? ProvinceName,
    IReadOnlyList<CompanyContact> Contacts,
    string Status,
    long RowVersion);

public sealed record CompanyContact(
    string Type,
    string FullName,
    string? Identification,
    string? Phone,
    string? Email,
    string? IdentificationType = null);

public sealed record CompaniesPage(
    IReadOnlyList<CompanySummary> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record CompanySearch(string? Search, string? Status, int Page, int PageSize);

public sealed record CompanyDraft(
    string LegalName,
    string TaxId,
    string? TradeName,
    string? EconomicActivity,
    string? Phone,
    string? Email,
    string? Address,
    Guid? MunicipalityId,
    IReadOnlyList<CompanyContact> Contacts,
    string Status,
    long? RowVersion);
