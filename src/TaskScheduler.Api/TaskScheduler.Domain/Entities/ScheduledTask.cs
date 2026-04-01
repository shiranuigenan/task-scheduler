namespace TaskScheduler.Domain.Entities;

public sealed class ScheduledTask
{
    public Guid Id { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string ParametersJson { get; set; } = "{}";
    public int IntervalMinutes { get; set; }
    public DateTime NextRunAt { get; set; }
    public bool IsActive { get; set; }
    public int RetryCount { get; set; }
    public string? GroupKey { get; set; }
    public DateTime? LastRunAt { get; set; }
}
