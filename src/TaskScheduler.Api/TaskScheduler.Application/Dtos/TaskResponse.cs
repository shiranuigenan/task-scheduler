namespace TaskScheduler.Application.Dtos;

public sealed record TaskResponse(
    Guid Id,
    string JobName,
    string ParametersJson,
    int IntervalMinutes,
    DateTime NextRunAt,
    bool IsActive,
    int RetryCount,
    string? GroupKey,
    DateTime? LastRunAt);
