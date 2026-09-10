using FluentValidation;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Application.Requests;

public sealed class InspectionRequestService(
    IInspectionRequestRepository repository,
    IValidator<InspectionRequestInput> validator)
{
    private static readonly string[] Readers =
    [
        "ADMINISTRADOR",
        "ADMINISTRADOR_EMPRESA",
        "USUARIO_DELEGADO",
        "COORDINADOR",
        "TECNICO_EVALUADOR"
    ];

    public Task<InspectionRequestsPage> SearchAsync(
        string? search,
        string? status,
        int page,
        int pageSize,
        InspectionRequestActor actor,
        CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        var globalScope = HasRole(actor, "ADMINISTRADOR") || HasRole(actor, "COORDINADOR");
        var assignedOnly = HasRole(actor, "TECNICO_EVALUADOR") && !globalScope;
        var companyScope = globalScope || assignedOnly ? (Guid?)null : RequiredCompany(actor);
        return repository.SearchAsync(
            new InspectionRequestSearch(
                Normalize(search),
                Normalize(status)?.ToUpperInvariant(),
                Math.Max(page, 1),
                Math.Clamp(pageSize, 5, 100),
                actor.UserId,
                companyScope,
                globalScope,
                assignedOnly),
            cancellationToken);
    }

    public Task<InspectionRequestOptions> GetOptionsAsync(
        InspectionRequestActor actor,
        CancellationToken cancellationToken)
    {
        EnsureReader(actor);
        var canManage = CanManage(actor);
        var globalScope = HasRole(actor, "ADMINISTRADOR") || HasRole(actor, "COORDINADOR");
        var companyScope = globalScope ? null : actor.CompanyId;
        return repository.GetOptionsAsync(companyScope, canManage, cancellationToken);
    }

    public async Task<Guid> CreateAsync(
        InspectionRequestInput input,
        InspectionRequestActor actor,
        CancellationToken cancellationToken)
    {
        EnsureManager(actor);
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        if (input.IdempotencyKey == Guid.Empty)
            throw new ArgumentException("La clave de idempotencia es obligatoria.");
        var companyId = ResolveCompany(input.CompanyId, actor);
        return await repository.CreateAsync(ToDraft(input, companyId), actor.UserId, cancellationToken);
    }

    public async Task UpdateAsync(
        Guid id,
        InspectionRequestInput input,
        InspectionRequestActor actor,
        CancellationToken cancellationToken)
    {
        EnsureManager(actor);
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        if (input.RowVersion is null) throw new ArgumentException("La versión de la solicitud es obligatoria.");
        var companyId = ResolveCompany(input.CompanyId, actor);
        if (!await repository.UpdateAsync(
                id,
                ToDraft(input, companyId),
                actor.UserId,
                CompanyWriteScope(actor),
                cancellationToken))
        {
            throw new OptimisticConcurrencyException(id);
        }
    }

    public async Task TransitionAsync(
        Guid id,
        InspectionRequestTransitionInput input,
        string targetStatus,
        InspectionRequestActor actor,
        CancellationToken cancellationToken)
    {
        EnsureManager(actor);
        if (input.RowVersion <= 0) throw new ArgumentException("La versión de la solicitud no es válida.");
        if (targetStatus is not ("ENVIADA" or "CANCELADA"))
            throw new ArgumentException("La transición solicitada no es válida.");
        if (!await repository.TransitionAsync(
                id,
                input.RowVersion,
                targetStatus,
                actor.UserId,
                CompanyWriteScope(actor),
                cancellationToken))
        {
            throw new OptimisticConcurrencyException(id);
        }
    }

    private static InspectionRequestDraft ToDraft(InspectionRequestInput input, Guid companyId) =>
        new(
            input.IdempotencyKey,
            companyId,
            input.EstablishmentId,
            input.InspectionReasonId,
            Normalize(input.ReasonDetail),
            Normalize(input.EstablishmentType),
            Normalize(input.Observations),
            input.RowVersion);

    private static Guid ResolveCompany(Guid? requested, InspectionRequestActor actor)
    {
        if (HasRole(actor, "ADMINISTRADOR") || HasRole(actor, "COORDINADOR"))
            return requested ?? throw new ArgumentException("Debe seleccionar una empresa.");
        var company = RequiredCompany(actor);
        if (requested.HasValue && requested != company)
            throw new ForbiddenException("No puede operar solicitudes de otra empresa.");
        return company;
    }

    private static Guid? CompanyWriteScope(InspectionRequestActor actor) =>
        HasRole(actor, "ADMINISTRADOR") || HasRole(actor, "COORDINADOR") ? null : RequiredCompany(actor);

    private static Guid RequiredCompany(InspectionRequestActor actor) =>
        actor.CompanyId ?? throw new ForbiddenException("El usuario no tiene un ámbito empresarial válido.");

    private static bool CanManage(InspectionRequestActor actor) =>
        HasRole(actor, "ADMINISTRADOR") ||
        HasRole(actor, "ADMINISTRADOR_EMPRESA") ||
        HasRole(actor, "USUARIO_DELEGADO") ||
        HasRole(actor, "COORDINADOR");

    private static void EnsureManager(InspectionRequestActor actor)
    {
        if (!CanManage(actor)) throw new ForbiddenException("No tiene permisos para modificar solicitudes.");
    }

    private static void EnsureReader(InspectionRequestActor actor)
    {
        if (!actor.Roles.Any(role => Readers.Contains(role, StringComparer.Ordinal)))
            throw new ForbiddenException("No tiene permisos para consultar solicitudes.");
    }

    private static bool HasRole(InspectionRequestActor actor, string role) =>
        actor.Roles.Contains(role, StringComparer.Ordinal);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
