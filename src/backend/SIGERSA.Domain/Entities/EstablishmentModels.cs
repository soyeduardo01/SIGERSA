namespace SIGERSA.Domain.Entities;

public sealed record EstablishmentSummary(
    Guid Id,
    string Code,
    string Name,
    Guid CompanyId,
    string CompanyName,
    string? ProvinceName,
    string? MunicipalityName,
    string? Phone,
    string? Email,
    string Status,
    long RowVersion);

public sealed record EstablishmentsPage(
    IReadOnlyList<EstablishmentSummary> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record EstablishmentSearch(string? Search, string? Status, int Page, int PageSize);

public sealed record EstablishmentContactDraft(
    string Type,
    string FullName,
    string? Identification,
    string? Phone,
    string? Email);

public sealed record EstablishmentProductDraft(
    Guid SubcategoryId,
    string Description,
    decimal? MonthlyVolume,
    string? Unit);

public sealed record EstablishmentDraft(
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
    IReadOnlyList<Guid> MarketIds,
    IReadOnlyList<EstablishmentContactDraft> Contacts,
    IReadOnlyList<EstablishmentProductDraft> Products,
    long? RowVersion);

public sealed record EstablishmentDetails(
    Guid Id,
    EstablishmentDraft Data,
    string CompanyName,
    Guid? ProvinceId);

public sealed record EstablishmentOption(Guid Id, string Code, string Name);
public sealed record MunicipalityOption(Guid Id, Guid ProvinceId, string Code, string Name);
public sealed record SubcategoryOption(Guid Id, Guid CategoryId, string Code, string Name, int? RiskLevel);
public sealed record EstablishmentOptions(
    IReadOnlyList<EstablishmentOption> Companies,
    IReadOnlyList<EstablishmentOption> Provinces,
    IReadOnlyList<MunicipalityOption> Municipalities,
    IReadOnlyList<EstablishmentOption> DpsDas,
    IReadOnlyList<EstablishmentOption> Commercializations,
    IReadOnlyList<EstablishmentOption> Markets,
    IReadOnlyList<EstablishmentOption> Categories,
    IReadOnlyList<SubcategoryOption> Subcategories,
    IReadOnlyList<ParameterControl> Statuses,
    IReadOnlyList<ParameterControl> HaccpLevels,
    IReadOnlyList<ParameterControl> SamplingApplications,
    IReadOnlyList<ParameterControl> InabieDistributions);
