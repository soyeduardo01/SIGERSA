using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Application.Operations;

public sealed record PublicComplaintRequest(
    Guid EstablishmentId,
    string ComplaintType,
    string Description,
    bool IsConfidential = true);

public sealed class PublicComplaintService(IOperationalRepository repository)
{
    public Task<IReadOnlyList<OperationalOption>> GetOptionsAsync(CancellationToken cancellationToken) =>
        repository.GetPublicComplaintOptionsAsync(cancellationToken);

    public Task<Guid> CreateAsync(PublicComplaintRequest request, CancellationToken cancellationToken)
    {
        if (request.EstablishmentId == Guid.Empty)
            throw new ArgumentException("Debe seleccionar el establecimiento relacionado.");
        var complaintType = request.ComplaintType?.Trim();
        var description = request.Description?.Trim();
        if (string.IsNullOrWhiteSpace(complaintType) || complaintType.Length > 100)
            throw new ArgumentException("El tipo de denuncia es obligatorio y admite hasta 100 caracteres.");
        if (string.IsNullOrWhiteSpace(description) || description.Length is < 20 or > 4000)
            throw new ArgumentException("La descripción debe contener entre 20 y 4000 caracteres.");
        return repository.CreatePublicComplaintAsync(
            new PublicComplaintDraft(request.EstablishmentId, complaintType, description, request.IsConfidential),
            cancellationToken);
    }
}
