using FluentValidation;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Application.Operations;

public sealed class OperationalService(
    IOperationalRepository repository,
    IValidator<SurveillanceRequest> surveillanceValidator,
    IValidator<FindingRequest> findingValidator)
{
    public Task<DashboardSnapshot> GetDashboardAsync(OperationalActor actor, CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        return repository.GetDashboardAsync(Scope(actor), cancellationToken);
    }

    public Task<IReadOnlyList<NotificationRecord>> GetNotificationsAsync(
        OperationalActor actor,
        CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        return repository.GetNotificationsAsync(actor.UserId, cancellationToken);
    }

    public async Task MarkNotificationReadAsync(
        Guid notificationId,
        OperationalActor actor,
        CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        if (!await repository.MarkNotificationReadAsync(notificationId, actor.UserId, cancellationToken))
            throw new KeyNotFoundException("La notificación no existe o todavía no está disponible.");
    }

    public Task<SurveillancePage> SearchSurveillanceAsync(
        string? search, string? kind, string? result, int page, int pageSize,
        OperationalActor actor, CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        return repository.SearchSurveillanceAsync(new SurveillanceSearch(
            Normalize(search)?.ToLowerInvariant(), Normalize(kind)?.ToUpperInvariant(),
            Normalize(result)?.ToUpperInvariant(), Math.Max(1, page),
            Math.Clamp(pageSize, 5, 100), Scope(actor)), cancellationToken);
    }

    public Task<SurveillanceOptions> GetSurveillanceOptionsAsync(OperationalActor actor, CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        return repository.GetSurveillanceOptionsAsync(CanCoordinate(actor), cancellationToken);
    }

    public async Task<Guid> CreateSurveillanceAsync(
        SurveillanceRequest request, OperationalActor actor, CancellationToken cancellationToken)
    {
        EnsureCoordinator(actor);
        await surveillanceValidator.ValidateAndThrowAsync(request, cancellationToken);
        return await repository.CreateSurveillanceAsync(ToDraft(request), actor.UserId, cancellationToken);
    }

    public async Task UpdateSurveillanceAsync(
        Guid id, SurveillanceRequest request, OperationalActor actor, CancellationToken cancellationToken)
    {
        EnsureCoordinator(actor);
        await surveillanceValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (!request.RowVersion.HasValue) throw new ArgumentException("La versión del registro es obligatoria.");
        if (!await repository.UpdateSurveillanceAsync(id, ToDraft(request), actor.UserId, cancellationToken))
            throw new OptimisticConcurrencyException(id);
    }

    public Task<FindingsPage> SearchFindingsAsync(
        string? search, string? status, int page, int pageSize,
        OperationalActor actor, CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        return repository.SearchFindingsAsync(new FindingSearch(
            Normalize(search)?.ToLowerInvariant(), Normalize(status)?.ToUpperInvariant(),
            Math.Max(1, page), Math.Clamp(pageSize, 5, 100), Scope(actor)), cancellationToken);
    }

    public Task<FindingOptions> GetFindingOptionsAsync(OperationalActor actor, CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        return repository.GetFindingOptionsAsync(actor.UserId, CanCreateFinding(actor), cancellationToken);
    }

    public async Task<Guid> CreateFindingAsync(
        FindingRequest request, OperationalActor actor, CancellationToken cancellationToken)
    {
        if (!CanCreateFinding(actor)) throw new ForbiddenException("No tiene permisos para crear hallazgos.");
        await findingValidator.ValidateAndThrowAsync(request, cancellationToken);
        return await repository.CreateFindingAsync(
            new FindingDraft(request.EvaluationId, request.SourceItem, request.CriticalityId, request.Description.Trim()),
            actor.UserId, cancellationToken);
    }

    public async Task CloseFindingAsync(
        Guid id, CloseFindingRequest request, OperationalActor actor, CancellationToken cancellationToken)
    {
        EnsureCoordinator(actor);
        if (request.RowVersion <= 0 || string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("La versión y la justificación son obligatorias.");
        if (!await repository.CloseFindingAsync(id, request.RowVersion, request.Reason.Trim(), actor.UserId, cancellationToken))
            throw new OptimisticConcurrencyException(id);
    }

    public Task<HistoricalEvaluationsPage> SearchHistoryAsync(
        string? search, string? status, DateTimeOffset? from, DateTimeOffset? to,
        int page, int pageSize, OperationalActor actor, CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        if (from.HasValue && to.HasValue && to < from) throw new ArgumentException("El rango de fechas no es válido.");
        return repository.SearchHistoryAsync(new HistoricalEvaluationSearch(
            Normalize(search)?.ToLowerInvariant(), Normalize(status)?.ToUpperInvariant(), from, to,
            Math.Max(1, page), Math.Clamp(pageSize, 5, 100), Scope(actor)), cancellationToken);
    }

    public Task<IReadOnlyList<TimelineEvent>> GetTimelineAsync(
        Guid evaluationId, OperationalActor actor, CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        return repository.GetTimelineAsync(evaluationId, Scope(actor), cancellationToken);
    }

    public Task<AuditEventsPage> SearchAuditAsync(
        string? search, string? result, DateTimeOffset? from, DateTimeOffset? to,
        int page, int pageSize, OperationalActor actor, CancellationToken cancellationToken)
    {
        if (!actor.Roles.Any(role => role is "ADMINISTRADOR" or "COORDINADOR"))
            throw new ForbiddenException("No tiene permisos para consultar la auditoría.");
        if (from.HasValue && to.HasValue && to < from) throw new ArgumentException("El rango de fechas no es válido.");
        return repository.SearchAuditAsync(new AuditEventSearch(
            Normalize(search)?.ToLowerInvariant(), Normalize(result)?.ToUpperInvariant(), from, to,
            Math.Max(1, page), Math.Clamp(pageSize, 5, 100)), cancellationToken);
    }

    private static SurveillanceDraft ToDraft(SurveillanceRequest request) => new(
        request.Kind.Trim().ToUpperInvariant(), request.OccurredAt, request.CompanyId,
        request.EstablishmentId, request.Subject.Trim(), request.Description.Trim(), request.Priority,
        Normalize(request.Channel)?.ToUpperInvariant(), request.IsAnonymous, request.IsConfidential,
        Normalize(request.Result)?.ToUpperInvariant(), request.RowVersion);

    private static OperationalActorScope Scope(OperationalActor actor)
    {
        var global = actor.Roles.Any(role => role is "ADMINISTRADOR" or "COORDINADOR");
        var assigned = !global && actor.Roles.Contains("TECNICO_EVALUADOR", StringComparer.Ordinal);
        Guid? company = global || assigned ? null : actor.CompanyId
            ?? throw new ForbiddenException("El usuario no tiene un ámbito empresarial válido.");
        return new OperationalActorScope(actor.UserId, company, global, assigned);
    }

    private static bool CanCoordinate(OperationalActor actor) =>
        actor.Roles.Any(role => role is "ADMINISTRADOR" or "COORDINADOR");

    private static bool CanCreateFinding(OperationalActor actor) =>
        actor.Roles.Any(role => role is "ADMINISTRADOR" or "TECNICO_EVALUADOR");

    private static void EnsureCoordinator(OperationalActor actor)
    {
        if (!CanCoordinate(actor)) throw new ForbiddenException("Solo un coordinador autorizado puede realizar esta operación.");
    }

    private static void EnsureReader(OperationalActor actor)
    {
        if (!actor.Roles.Any(role => role is "ADMINISTRADOR" or "ADMINISTRADOR_EMPRESA" or "USUARIO_DELEGADO" or "COORDINADOR" or "TECNICO_EVALUADOR"))
            throw new ForbiddenException("No tiene permisos para consultar este módulo.");
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
