using FluentValidation;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Application.Corrections;

public sealed class CorrectionService(ICorrectionRepository repository, IValidator<CorrectionInput> validator)
{
    public Task<CorrectionsPage> SearchAsync(string? search, string? status, int page, int pageSize,
        CorrectionActor actor, CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        var global = HasRole(actor, "ADMINISTRADOR") || HasRole(actor, "COORDINADOR");
        var assigned = HasRole(actor, "TECNICO_EVALUADOR") && !global;
        var company = global || assigned ? (Guid?)null : actor.CompanyId
            ?? throw new ForbiddenException("El usuario no tiene un ámbito empresarial válido.");
        return repository.SearchAsync(new CorrectionSearch(
            Normalize(search)?.ToLowerInvariant(), Normalize(status)?.ToUpperInvariant(),
            Math.Max(page, 1), Math.Clamp(pageSize, 5, 100), actor.UserId,
            company, global, assigned), cancellationToken);
    }

    public Task<CorrectionOptions> GetOptionsAsync(CorrectionActor actor, CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        return repository.GetOptionsAsync(CanReview(actor), cancellationToken);
    }

    public async Task<Guid> CreateAsync(CorrectionInput input, CorrectionActor actor, CancellationToken cancellationToken)
    {
        EnsureReviewer(actor);
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        return await repository.CreateAsync(new CorrectionDraft(
            input.IdempotencyKey, input.EvaluationId, input.ResponsibleType,
            input.AssignedToId, input.CoordinatorObservation.Trim(), input.DueAt,
            input.Fields?.Select(field => new CorrectionFieldDraft(
                field.SourceItem, field.Reason.Trim())).ToArray() ?? []), actor.UserId, cancellationToken);
    }

    public async Task SubmitAsync(Guid id, CorrectionTransitionInput input, CorrectionActor actor, CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        if (input.RowVersion <= 0) throw new ArgumentException("La versión de la corrección es obligatoria.");
        var global = HasRole(actor, "ADMINISTRADOR");
        var assigned = HasRole(actor, "TECNICO_EVALUADOR") && !global;
        var company = global || assigned ? (Guid?)null : actor.CompanyId;
        if (!global && !assigned && !HasRole(actor, "ADMINISTRADOR_EMPRESA") && !HasRole(actor, "USUARIO_DELEGADO"))
            throw new ForbiddenException("No tiene permisos para responder correcciones.");
        if (!await repository.SubmitAsync(id, input.RowVersion, actor.UserId, company, global, assigned, cancellationToken))
            throw new OptimisticConcurrencyException(id);
    }

    public async Task ResolveAsync(Guid id, CorrectionTransitionInput input, string targetStatus,
        CorrectionActor actor, CancellationToken cancellationToken)
    {
        EnsureReviewer(actor);
        if (input.RowVersion <= 0) throw new ArgumentException("La versión de la corrección es obligatoria.");
        if (targetStatus is not ("ACEPTADA" or "RECHAZADA")) throw new ArgumentException("Estado de resolución no válido.");
        if (!await repository.ResolveAsync(id, input.RowVersion, targetStatus, actor.UserId, cancellationToken))
            throw new OptimisticConcurrencyException(id);
    }

    private static bool CanReview(CorrectionActor actor) => HasRole(actor, "ADMINISTRADOR") || HasRole(actor, "COORDINADOR");
    private static void EnsureReviewer(CorrectionActor actor) { if (!CanReview(actor)) throw new ForbiddenException("Solo un coordinador autorizado puede solicitar o resolver correcciones."); }
    private static void EnsureReader(CorrectionActor actor)
    {
        if (!actor.Roles.Any(role => role is "ADMINISTRADOR" or "ADMINISTRADOR_EMPRESA" or "USUARIO_DELEGADO" or "COORDINADOR" or "TECNICO_EVALUADOR"))
            throw new ForbiddenException("No tiene permisos para consultar correcciones.");
    }
    private static bool HasRole(CorrectionActor actor, string role) => actor.Roles.Contains(role, StringComparer.Ordinal);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
