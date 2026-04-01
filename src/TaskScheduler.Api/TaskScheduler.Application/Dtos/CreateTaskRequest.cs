using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace TaskScheduler.Application.Dtos;

public sealed class CreateTaskRequest
{
    [Required]
    [MinLength(1)]
    public string JobName { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int IntervalMinutes { get; set; }

    public string? GroupKey { get; set; }

    /// <summary>Optional JSON object; stored as ParametersJson.</summary>
    public JsonElement? Parameters { get; set; }

    [Range(0, 100)]
    public int RetryCount { get; set; } = 3;
}
