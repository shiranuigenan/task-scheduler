namespace TaskScheduler.Application.Jobs;

public interface IJobFactory
{
    IJobHandler? Resolve(string jobName);
    IReadOnlyCollection<string> GetRegisteredJobNames();
}
