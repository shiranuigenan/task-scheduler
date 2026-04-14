using Microsoft.EntityFrameworkCore;
using TaskScheduler.Domain.Entities;

namespace TaskScheduler.Infrastructure.Persistence;

public sealed class ScheduledTaskRepository(AppDbContext db)
{
    public IReadOnlyList<ScheduledTask> GetAll(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return db.ScheduledTasks.AsNoTracking().OrderBy(t => t.NextRunAt).ToList();
    }

    public IReadOnlyList<ScheduledTask> GetDueTasks(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTime.UtcNow;
        return db.ScheduledTasks.AsNoTracking()
            .Where(t => t.IsActive && t.NextRunAt <= now)
            .OrderBy(t => t.NextRunAt)
            .ToList();
    }

    public ScheduledTask? GetById(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return db.ScheduledTasks.FirstOrDefault(t => t.Id == id);
    }

    public void Add(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.ScheduledTasks.Add(task);
        db.SaveChanges();
    }

    public void Update(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.ScheduledTasks.Update(task);
        db.SaveChanges();
    }

    public bool Delete(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var affected = db.ScheduledTasks.Where(t => t.Id == id).ExecuteDelete();
        return affected > 0;
    }
}
