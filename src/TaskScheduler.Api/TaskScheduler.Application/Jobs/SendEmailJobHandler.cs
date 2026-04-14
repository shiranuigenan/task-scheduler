using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TaskScheduler.Application.Jobs;

public sealed class SendEmailJobHandler(ILogger<SendEmailJobHandler> logger) : IJobHandler
{
    public string Name => "SendEmail";

    public void Execute(string parametersJson, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var parameters = JsonSerializer.Deserialize<SendEmailParameters>(parametersJson, options)
            ?? throw new JsonException("SendEmail parameters cannot be null.");

        if (string.IsNullOrWhiteSpace(parameters.To) || string.IsNullOrWhiteSpace(parameters.Subject))
            throw new JsonException("SendEmail requires non-empty 'to' and 'subject'.");

        logger.LogWarning("SendEmail job: to={To}, subject={Subject}", parameters.To, parameters.Subject);
    }

    private sealed record SendEmailParameters(string To, string Subject);
}
