using SIGERSA.Application.Scheduling;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Tests.Application;

public sealed class SchedulingServiceTests
{
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task DelegateCannotReadOrModifyScheduling()
    {
        var service = CreateService(new FakeRepository());
        var actor = new SchedulingActor(UserId, ["USUARIO_DELEGADO"]);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.SearchAsync(null, null, 1, 10, actor, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateAsync(ValidInput(), actor, CancellationToken.None));
    }

    [Fact]
    public async Task CoordinatorCanCreateValidSchedule()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);
        var input = ValidInput();

        await service.CreateAsync(input, new SchedulingActor(UserId, ["COORDINADOR"]), CancellationToken.None);

        Assert.Equal(input.CaseId, repository.LastDraft?.CaseId);
        Assert.Equal(UserId, repository.LastActorId);
    }

    [Fact]
    public async Task UpdateRequiresChangeReasonAndRowVersion()
    {
        var service = CreateService(new FakeRepository());
        var input = ValidInput() with { RowVersion = 1, ChangeReason = null };

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(
            Guid.NewGuid(), input, new SchedulingActor(UserId, ["COORDINADOR"]), CancellationToken.None));
    }

    [Fact]
    public async Task CancelConflictIsExposedAsOptimisticConcurrency()
    {
        var service = CreateService(new FakeRepository { CancelResult = false });

        await Assert.ThrowsAsync<OptimisticConcurrencyException>(() => service.CancelAsync(
            Guid.NewGuid(), new CancelScheduleInput(1, "Cambio operativo"),
            new SchedulingActor(UserId, ["ADMINISTRADOR"]), CancellationToken.None));
    }

    [Fact]
    public void EndValidationComparesTheActualInstantAcrossTimeZones()
    {
        var validator = new ScheduleInputValidator();
        var start = new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.FromHours(-4));
        var sameInstant = new DateTimeOffset(2026, 9, 15, 13, 0, 0, TimeSpan.Zero);
        var laterInstant = sameInstant.AddMinutes(1);

        Assert.False(validator.Validate(ValidInput() with { StartsAt = start, EndsAt = sameInstant }).IsValid);
        Assert.True(validator.Validate(ValidInput() with { StartsAt = start, EndsAt = laterInstant }).IsValid);
    }

    private static SchedulingService CreateService(FakeRepository repository) =>
        new(repository, new ScheduleInputValidator());

    private static ScheduleInput ValidInput()
    {
        var start = DateTimeOffset.UtcNow.AddDays(1);
        return new ScheduleInput(Guid.NewGuid(), start, start.AddHours(2), 2,
            "Visita", null, [Guid.NewGuid()], Guid.NewGuid(), null);
    }

    private sealed class FakeRepository : ISchedulingRepository
    {
        public ScheduleDraft? LastDraft { get; private set; }
        public Guid? LastActorId { get; private set; }
        public bool CancelResult { get; init; } = true;

        public Task<SchedulesPage> SearchAsync(ScheduleSearch query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SchedulesPage([], query.Page, query.PageSize, 0));

        public Task<ScheduleOptions> GetOptionsAsync(bool canManage, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScheduleOptions([], [], canManage));

        public Task<Guid> CreateAsync(ScheduleDraft draft, Guid actorId, CancellationToken cancellationToken = default)
        {
            LastDraft = draft;
            LastActorId = actorId;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<bool> UpdateAsync(Guid id, ScheduleDraft draft, Guid actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> CancelAsync(Guid id, long rowVersion, string reason, Guid actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(CancelResult);
    }
}
