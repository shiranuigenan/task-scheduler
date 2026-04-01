using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TaskScheduler.Application.Jobs;

public sealed class CleanupJobHandler(ILogger<CleanupJobHandler> logger) : IJobHandler
{
    public string Name => "Cleanup";

    public Task ExecuteAsync(string parametersJson, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var parameters = JsonSerializer.Deserialize<CleanupParameters>(parametersJson, options)
            ?? throw new JsonException("Cleanup parameters cannot be null.");

        if (parameters.Days < 0)
            throw new JsonException("Cleanup 'days' must be non-negative.");

        logger.LogInformation("Cleanup job: remove data older than {Days} days", parameters.Days);
        return Task.CompletedTask;
    }

    private sealed record CleanupParameters(int Days);
}
