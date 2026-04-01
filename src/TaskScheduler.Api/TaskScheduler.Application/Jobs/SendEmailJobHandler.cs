using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TaskScheduler.Application.Jobs;

public sealed class SendEmailJobHandler(ILogger<SendEmailJobHandler> logger) : IJobHandler
{
    public string Name => "SendEmail";

    public Task ExecuteAsync(string parametersJson, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var parameters = JsonSerializer.Deserialize<SendEmailParameters>(parametersJson, options)
            ?? throw new JsonException("SendEmail parameters cannot be null.");

        if (string.IsNullOrWhiteSpace(parameters.To) || string.IsNullOrWhiteSpace(parameters.Subject))
            throw new JsonException("SendEmail requires non-empty 'to' and 'subject'.");

        logger.LogInformation("SendEmail job: to={To}, subject={Subject}", parameters.To, parameters.Subject);
        return Task.CompletedTask;
    }

    private sealed record SendEmailParameters(string To, string Subject);
}
