using Microsoft.Extensions.Logging;
using SIGERSA.Application.Notifications;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Application.Operations;

public sealed record PublicComplaintRequest(
    Guid EstablishmentId,
    string ComplaintType,
    string Description,
    bool IsConfidential = true);

public sealed partial class PublicComplaintService(
    IOperationalRepository repository,
    WebPushNotificationService webPush,
    ILogger<PublicComplaintService> logger)
{
    public Task<IReadOnlyList<OperationalOption>> GetOptionsAsync(CancellationToken cancellationToken) =>
        repository.GetPublicComplaintOptionsAsync(cancellationToken);

    public async Task<Guid> CreateAsync(PublicComplaintRequest request, CancellationToken cancellationToken)
    {
        if (request.EstablishmentId == Guid.Empty)
            throw new ArgumentException("Debe seleccionar el establecimiento relacionado.");
        var complaintType = request.ComplaintType?.Trim();
        var description = request.Description?.Trim();
        if (string.IsNullOrWhiteSpace(complaintType) || complaintType.Length > 100)
            throw new ArgumentException("El tipo de denuncia es obligatorio y admite hasta 100 caracteres.");
        if (string.IsNullOrWhiteSpace(description) || description.Length is < 20 or > 4000)
            throw new ArgumentException("La descripción debe contener entre 20 y 4000 caracteres.");
        var created = await repository.CreatePublicComplaintAsync(
            new PublicComplaintDraft(request.EstablishmentId, complaintType, description, request.IsConfidential),
            cancellationToken);

        try
        {
            var delivery = await webPush.SendToUserAsync(
                created.CoordinatorId,
                new SendWebPushRequest(
                    "SIGERSA · Nueva denuncia ciudadana asignada",
                    "Se recibió una denuncia desde el portal público y requiere revisión de coordinación.",
                    "/modulo.html?module=alertas-denuncias",
                    $"denuncia-{created.Id:N}",
                    RequireInteraction: true),
                cancellationToken);
            LogPushDelivery(logger, created.Id, delivery.Sent, delivery.Subscriptions,
                delivery.Expired, delivery.Failed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // La denuncia ya fue confirmada en la base de datos. Un canal secundario
            // no debe convertir ese éxito en un error ni provocar una denuncia duplicada.
            LogPushFailure(logger, created.Id, exception);
        }

        return created.Id;
    }

    [LoggerMessage(1001, LogLevel.Information,
        "Web Push de denuncia {ComplaintId}: {Sent}/{Subscriptions} entregas aceptadas, {Expired} expiradas y {Failed} fallidas.")]
    private static partial void LogPushDelivery(
        ILogger logger, Guid complaintId, int sent, int subscriptions, int expired, int failed);

    [LoggerMessage(1002, LogLevel.Warning,
        "No se pudo enviar Web Push para la denuncia {ComplaintId}.")]
    private static partial void LogPushFailure(ILogger logger, Guid complaintId, Exception exception);
}
