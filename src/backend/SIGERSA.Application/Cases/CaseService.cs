using FluentValidation;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Application.Cases;

public sealed class CaseService(ICaseRepository repository, IValidator<CaseInput> validator)
{
    public Task<CasesPage> SearchAsync(
        string? search,
        string? status,
        int page,
        int pageSize,
        CaseActor actor,
        CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        var global = HasRole(actor, "ADMINISTRADOR") || HasRole(actor, "COORDINADOR");
        var assigned = HasRole(actor, "TECNICO_EVALUADOR") && !global;
        var company = global || assigned ? (Guid?)null : RequiredCompany(actor);
        return repository.SearchAsync(new CaseSearch(
            Normalize(search)?.ToLowerInvariant(), Normalize(status)?.ToUpperInvariant(),
            Math.Max(1, page), Math.Clamp(pageSize, 5, 100), actor.UserId, company, global, assigned), cancellationToken);
    }

    public Task<CaseOptions> GetOptionsAsync(CaseActor actor, CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        return repository.GetOptionsAsync(CanManage(actor), cancellationToken);
    }

    public async Task<Guid> CreateAsync(CaseInput input, CaseActor actor, CancellationToken cancellationToken)
    {
        EnsureManager(actor);
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        if (input.IdempotencyKey == Guid.Empty) throw new ArgumentException("La clave de idempotencia es obligatoria.");
        return await repository.CreateAsync(ToDraft(input), actor.UserId, cancellationToken);
    }

    public async Task UpdateAsync(Guid id, CaseInput input, CaseActor actor, CancellationToken cancellationToken)
    {
        EnsureManager(actor);
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        if (!input.RowVersion.HasValue) throw new ArgumentException("La versión del caso es obligatoria.");
        if (!await repository.UpdateAsync(id, ToDraft(input), actor.UserId, cancellationToken))
            throw new OptimisticConcurrencyException(id);
    }

    public async Task CloseAsync(Guid id, CloseCaseInput input, CaseActor actor, CancellationToken cancellationToken)
    {
        EnsureManager(actor);
        if (input.RowVersion <= 0 || string.IsNullOrWhiteSpace(input.Reason))
            throw new ArgumentException("La versión y la justificación son obligatorias para cerrar el caso.");
        if (!await repository.CloseAsync(id, input.RowVersion, input.Reason.Trim(), actor.UserId, cancellationToken))
            throw new OptimisticConcurrencyException(id);
    }

    private static CaseDraft ToDraft(CaseInput value) => new(
        value.IdempotencyKey, value.Origin.Trim().ToUpperInvariant(), value.SourceId,
        value.Priority, value.ResponsibleId,
        Normalize(value.AnalysisDecision)?.ToUpperInvariant(), Normalize(value.DecisionReason), value.RowVersion);

    private static bool CanManage(CaseActor actor) => HasRole(actor, "ADMINISTRADOR") || HasRole(actor, "COORDINADOR");
    private static void EnsureManager(CaseActor actor)
    {
        if (!CanManage(actor)) throw new ForbiddenException("Solo un coordinador autorizado puede administrar casos.");
    }
    private static void EnsureReader(CaseActor actor)
    {
        if (!actor.Roles.Any(role => role is "ADMINISTRADOR" or "ADMINISTRADOR_EMPRESA" or "USUARIO_DELEGADO" or "COORDINADOR" or "TECNICO_EVALUADOR"))
            throw new ForbiddenException("No tiene permisos para consultar casos.");
    }
    private static Guid RequiredCompany(CaseActor actor) =>
        actor.CompanyId ?? throw new ForbiddenException("El usuario no tiene un ámbito empresarial válido.");
    private static bool HasRole(CaseActor actor, string role) => actor.Roles.Contains(role, StringComparer.Ordinal);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
