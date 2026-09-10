using SIGERSA.Domain.Entities;

namespace SIGERSA.Domain.Repositories;

public interface ISchedulingRepository
{
    Task<SchedulesPage> SearchAsync(ScheduleSearch query, CancellationToken cancellationToken = default);
    Task<ScheduleOptions> GetOptionsAsync(bool canManage, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(ScheduleDraft draft, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Guid id, ScheduleDraft draft, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> CancelAsync(Guid id, long rowVersion, string reason, Guid actorId, CancellationToken cancellationToken = default);
}
