using TaskScheduler.Application.Dtos;
using TaskScheduler.Application.Jobs;
using TaskScheduler.Domain.Entities;
using TaskScheduler.Infrastructure.Persistence;

namespace TaskScheduler.Application.Services;

public sealed class ScheduledTaskService(
    ScheduledTaskRepository repository,
    JobFactory jobFactory)
{
    public IReadOnlyList<TaskResponse> GetAll(CancellationToken cancellationToken = default)
    {
        var items = repository.GetAll(cancellationToken);
        return items.Select(Map).ToList();
    }

    public TaskResponse Create(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        if (jobFactory.Resolve(request.JobName) is null)
            throw new InvalidOperationException($"Unknown job: '{request.JobName}'.");

        var now = DateTime.UtcNow;

        var parametersJson = request.Parameters?.GetRawText() ?? "{}";

        var entity = new ScheduledTask
        {
            Id = Guid.NewGuid(),
            JobName = request.JobName,
            ParametersJson = parametersJson,
            IntervalMinutes = request.IntervalMinutes,
            NextRunAt = now,
            IsActive = false,
            RetryCount = Math.Max(0, request.RetryCount),
            GroupKey = string.IsNullOrWhiteSpace(request.GroupKey) ? null : request.GroupKey.Trim(),
        };

        repository.Add(entity, cancellationToken);
        return Map(entity);
    }

    public void Activate(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = repository.GetById(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Task '{id}' was not found.");

        entity.IsActive = true;
        var now = DateTime.UtcNow;

        if (entity.NextRunAt <= now)
            entity.NextRunAt = now;

        repository.Update(entity, cancellationToken);
    }

    public void Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!repository.Delete(id, cancellationToken))
            throw new KeyNotFoundException($"Task '{id}' was not found.");
    }

    private static TaskResponse Map(ScheduledTask task) =>
        new(
            task.Id,
            task.JobName,
            task.ParametersJson,
            task.IntervalMinutes,
            task.NextRunAt,
            task.IsActive,
            task.RetryCount,
            task.GroupKey,
            task.LastRunAt);
}
