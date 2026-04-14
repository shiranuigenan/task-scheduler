namespace TaskScheduler.Application.Jobs;

public interface IJobHandler
{
    string Name { get; }
    void Execute(string parametersJson, CancellationToken cancellationToken);
}
