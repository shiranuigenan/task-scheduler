namespace TaskScheduler.Application.Jobs;

public interface IJobHandler
{
    string Name { get; }
    Task ExecuteAsync(string parametersJson, CancellationToken cancellationToken);
}
