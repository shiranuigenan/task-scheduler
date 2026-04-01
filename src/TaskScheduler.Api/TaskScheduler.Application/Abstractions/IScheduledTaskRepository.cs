using TaskScheduler.Domain.Entities;

namespace TaskScheduler.Application.Abstractions;

public interface IScheduledTaskRepository
{
    Task<IReadOnlyList<ScheduledTask>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduledTask>> GetDueTasksAsync(CancellationToken cancellationToken = default);
    Task<ScheduledTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(ScheduledTask task, CancellationToken cancellationToken = default);
    Task UpdateAsync(ScheduledTask task, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
