using FluentValidation;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Application.Scheduling;

public sealed class SchedulingService(ISchedulingRepository repository, IValidator<ScheduleInput> validator)
{
    public Task<SchedulesPage> SearchAsync(string? search, string? status, int page, int pageSize,
        SchedulingActor actor, CancellationToken cancellationToken)
    {
        EnsureAccess(actor);
        return repository.SearchAsync(new ScheduleSearch(Normalize(search)?.ToLowerInvariant(),
            Normalize(status)?.ToUpperInvariant(), Math.Max(page, 1), Math.Clamp(pageSize, 5, 100)), cancellationToken);
    }

    public Task<ScheduleOptions> GetOptionsAsync(SchedulingActor actor, CancellationToken cancellationToken)
    {
        EnsureAccess(actor);
        return repository.GetOptionsAsync(true, cancellationToken);
    }

    public async Task<Guid> CreateAsync(ScheduleInput input, SchedulingActor actor, CancellationToken cancellationToken)
    {
        EnsureAccess(actor);
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        if (input.IdempotencyKey == Guid.Empty) throw new ArgumentException("La clave de idempotencia es obligatoria.");
        return await repository.CreateAsync(ToDraft(input), actor.UserId, cancellationToken);
    }

    public async Task UpdateAsync(Guid id, ScheduleInput input, SchedulingActor actor, CancellationToken cancellationToken)
    {
        EnsureAccess(actor);
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        if (!input.RowVersion.HasValue || string.IsNullOrWhiteSpace(input.ChangeReason))
            throw new ArgumentException("La versión y el motivo de reprogramación son obligatorios.");
        if (!await repository.UpdateAsync(id, ToDraft(input), actor.UserId, cancellationToken))
            throw new OptimisticConcurrencyException(id);
    }

    public async Task CancelAsync(Guid id, CancelScheduleInput input, SchedulingActor actor, CancellationToken cancellationToken)
    {
        EnsureAccess(actor);
        if (input.RowVersion <= 0 || string.IsNullOrWhiteSpace(input.Reason))
            throw new ArgumentException("La versión y el motivo de cancelación son obligatorios.");
        if (!await repository.CancelAsync(id, input.RowVersion, input.Reason.Trim(), actor.UserId, cancellationToken))
            throw new OptimisticConcurrencyException(id);
    }

    private static ScheduleDraft ToDraft(ScheduleInput input) => new(
        input.IdempotencyKey, input.CaseId, input.StartsAt, input.EndsAt, input.Priority,
        Normalize(input.Observations), Normalize(input.ChangeReason), input.EvaluatorIds, input.RowVersion);
    private static void EnsureAccess(SchedulingActor actor)
    {
        if (!actor.Roles.Any(role => role is "ADMINISTRADOR" or "COORDINADOR"))
            throw new ForbiddenException("Solo un coordinador autorizado puede administrar la programación.");
    }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
