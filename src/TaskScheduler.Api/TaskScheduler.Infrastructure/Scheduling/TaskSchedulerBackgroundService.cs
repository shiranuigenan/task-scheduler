using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskScheduler.Application.Abstractions;
using TaskScheduler.Application.Jobs;
using TaskScheduler.Domain.Entities;

namespace TaskScheduler.Infrastructure.Scheduling;

/// <summary>
/// Polls due tasks, enforces group locks, runs each execution in a new DI scope with retries; releases lock only after completion.
/// </summary>
public sealed class TaskSchedulerBackgroundService(
    IServiceScopeFactory scopeFactory,
    IGroupLockManager groupLockManager,
    ILogger<TaskSchedulerBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Task scheduler started; polling every {Seconds}s", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduler poll cycle failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("Task scheduler stopped");
    }

    private async Task PollOnceAsync(CancellationToken stoppingToken)
    {
        using var pollScope = scopeFactory.CreateScope();
        var repository = pollScope.ServiceProvider.GetRequiredService<IScheduledTaskRepository>();
        var due = await repository.GetDueTasksAsync(stoppingToken).ConfigureAwait(false);

        foreach (var snapshot in due)
        {
            var groupKey = ResolveGroupKey(snapshot);
            var lease = await groupLockManager.TryAcquireAsync(groupKey, stoppingToken).ConfigureAwait(false);
            if (lease is null)
                continue;

            var taskId = snapshot.Id;
            _ = RunTaskSafeAsync(taskId, lease, stoppingToken);
        }
    }

    private static string ResolveGroupKey(ScheduledTask task) =>
        string.IsNullOrWhiteSpace(task.GroupKey) ? task.Id.ToString() : task.GroupKey.Trim();

    private async Task RunTaskSafeAsync(Guid taskId, IAsyncDisposable groupLease, CancellationToken stoppingToken)
    {
        try
        {
            await using (groupLease)
            {
                await RunTaskCoreAsync(taskId, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error running scheduled task {TaskId}", taskId);
        }
    }

    private async Task RunTaskCoreAsync(Guid taskId, CancellationToken stoppingToken)
    {
        await using var execScope = scopeFactory.CreateAsyncScope();
        var sp = execScope.ServiceProvider;
        var repository = sp.GetRequiredService<IScheduledTaskRepository>();
        var jobFactory = sp.GetRequiredService<IJobFactory>();
        var log = sp.GetRequiredService<ILogger<TaskSchedulerBackgroundService>>();

        var task = await repository.GetByIdAsync(taskId, stoppingToken).ConfigureAwait(false);
        if (task is null || !task.IsActive)
            return;

        var now = DateTime.UtcNow;
        if (task.NextRunAt > now)
            return;

        var handler = jobFactory.Resolve(task.JobName);
        if (handler is null)
        {
            log.LogError("No handler registered for job {JobName} (task {TaskId})", task.JobName, taskId);
            AdvanceSchedule(task, now);
            await repository.UpdateAsync(task, stoppingToken).ConfigureAwait(false);
            return;
        }

        var maxAttempts = Math.Max(1, task.RetryCount);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            stoppingToken.ThrowIfCancellationRequested();
            try
            {
                await handler.ExecuteAsync(task.ParametersJson, stoppingToken).ConfigureAwait(false);
                lastError = null;
                break;
            }
            catch (Exception ex)
            {
                lastError = ex;
                log.LogWarning(ex, "Job {JobName} task {TaskId} attempt {Attempt}/{Max} failed",
                    task.JobName, taskId, attempt, maxAttempts);
            }
        }

        if (lastError is not null)
            log.LogError(lastError, "Job {JobName} task {TaskId} exhausted retries", task.JobName, taskId);

        AdvanceSchedule(task, DateTime.UtcNow);
        await repository.UpdateAsync(task, stoppingToken).ConfigureAwait(false);
    }

    private static void AdvanceSchedule(ScheduledTask task, DateTime now)
    {
        task.LastRunAt = now;
        task.NextRunAt = now.AddMinutes(task.IntervalMinutes);
    }
}
