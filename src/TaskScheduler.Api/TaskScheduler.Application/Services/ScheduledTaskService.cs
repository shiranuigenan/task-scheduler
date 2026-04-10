using TaskScheduler.Application.Dtos;
using TaskScheduler.Application.Jobs;
using TaskScheduler.Domain.Entities;
using TaskScheduler.Infrastructure.Persistence;

namespace TaskScheduler.Application.Services;

public sealed class ScheduledTaskService(
    ScheduledTaskRepository repository,
    JobFactory jobFactory)
{
    public async Task<IReadOnlyList<TaskResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await repository.GetAllAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<TaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
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

        await repository.AddAsync(entity, cancellationToken);
        return Map(entity);
    }

    public async Task ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Task '{id}' was not found.");

        entity.IsActive = true;
        var now = DateTime.UtcNow;

        if (entity.NextRunAt <= now)
            entity.NextRunAt = now;

        await repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await repository.DeleteAsync(id, cancellationToken))
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
