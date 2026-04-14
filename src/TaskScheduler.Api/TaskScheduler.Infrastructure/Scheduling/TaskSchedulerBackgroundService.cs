using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskScheduler.Application.Jobs;
using TaskScheduler.Domain.Entities;
using TaskScheduler.Infrastructure.Persistence;

namespace TaskScheduler.Infrastructure.Scheduling;

public sealed class TaskSchedulerBackgroundService(
    IServiceScopeFactory scopeFactory,
    GroupLockManager groupLockManager,
    ILogger<TaskSchedulerBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Task scheduler started; polling every {Seconds}s", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                PollOnce(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduler poll cycle failed");
            }

            if (stoppingToken.WaitHandle.WaitOne(PollInterval))
                break;
        }

        logger.LogInformation("Task scheduler stopped");
        return Task.CompletedTask;
    }

    private void PollOnce(CancellationToken stoppingToken)
    {
        using var pollScope = scopeFactory.CreateScope();
        var repository = pollScope.ServiceProvider.GetRequiredService<ScheduledTaskRepository>();
        var due = repository.GetDueTasks(stoppingToken);

        foreach (var snapshot in due)
        {
            var groupKey = ResolveGroupKey(snapshot);

            var lease = groupLockManager.TryAcquire(groupKey, stoppingToken);
            if (lease is null)
                continue;

            var taskId = snapshot.Id;

            _ = Task.Run(() => RunTaskSafe(taskId, lease, stoppingToken), stoppingToken);
        }
    }

    private static string ResolveGroupKey(ScheduledTask task) =>
        string.IsNullOrWhiteSpace(task.GroupKey) ? task.Id.ToString() : task.GroupKey.Trim();

    private void RunTaskSafe(Guid taskId, IDisposable groupLease, CancellationToken stoppingToken)
    {
        try
        {
            using (groupLease)
            {
                RunTaskCore(taskId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error running scheduled task {TaskId}", taskId);
        }
    }

    private void RunTaskCore(Guid taskId, CancellationToken stoppingToken)
    {
        using var execScope = scopeFactory.CreateScope();
        var sp = execScope.ServiceProvider;
        var repository = sp.GetRequiredService<ScheduledTaskRepository>();
        var jobFactory = sp.GetRequiredService<JobFactory>();
        var log = sp.GetRequiredService<ILogger<TaskSchedulerBackgroundService>>();

        var task = repository.GetById(taskId, stoppingToken);
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
            repository.Update(task, stoppingToken);
            return;
        }

        var maxAttempts = Math.Max(1, task.RetryCount);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            stoppingToken.ThrowIfCancellationRequested();
            try
            {
                handler.Execute(task.ParametersJson, stoppingToken);
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
        repository.Update(task, stoppingToken);
    }

    private static void AdvanceSchedule(ScheduledTask task, DateTime now)
    {
        task.LastRunAt = now;
        task.NextRunAt = now.AddMinutes(task.IntervalMinutes);
    }
}
