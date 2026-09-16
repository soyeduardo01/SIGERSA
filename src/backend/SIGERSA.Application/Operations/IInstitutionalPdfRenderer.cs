using SIGERSA.Domain.Entities;

namespace SIGERSA.Application.Operations;

public interface IInstitutionalPdfRenderer
{
    Task<byte[]> RenderAsync(
        ReportGenerationData data,
        bool official,
        CancellationToken cancellationToken = default);
}

public sealed record InstitutionalReportAssets(
    string SigersaLogoDataUri,
    string DigemapsLogoDataUri,
    string PoppinsRegularDataUri,
    string PoppinsSemiBoldDataUri);
