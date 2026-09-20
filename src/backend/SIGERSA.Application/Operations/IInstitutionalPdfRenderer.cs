using SIGERSA.Domain.Entities;

namespace SIGERSA.Application.Operations;

public interface IInstitutionalPdfRenderer
{
    Task<byte[]> RenderAsync(
        ReportGenerationData data,
        bool official,
        string verificationUrl,
        CancellationToken cancellationToken = default);
}

public sealed record InstitutionalReportAssets(
    string SigersaLogoDataUri,
    string PoppinsRegularDataUri,
    string PoppinsSemiBoldDataUri);
