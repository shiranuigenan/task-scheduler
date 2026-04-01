using Microsoft.EntityFrameworkCore;
using TaskScheduler.Application.Abstractions;
using TaskScheduler.Domain.Entities;

namespace TaskScheduler.Infrastructure.Persistence;

public sealed class ScheduledTaskRepository(AppDbContext db) : IScheduledTaskRepository
{
    public async Task<IReadOnlyList<ScheduledTask>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.ScheduledTasks.AsNoTracking().OrderBy(t => t.NextRunAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ScheduledTask>> GetDueTasksAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await db.ScheduledTasks.AsNoTracking()
            .Where(t => t.IsActive && t.NextRunAt <= now)
            .OrderBy(t => t.NextRunAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ScheduledTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.ScheduledTasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task AddAsync(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        db.ScheduledTasks.Add(task);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        db.ScheduledTasks.Update(task);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var affected = await db.ScheduledTasks.Where(t => t.Id == id).ExecuteDeleteAsync(cancellationToken);
        return affected > 0;
    }
}
