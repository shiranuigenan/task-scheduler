namespace TaskScheduler.Application.Jobs;

public sealed class JobFactory : IJobFactory
{
    private readonly IReadOnlyDictionary<string, IJobHandler> _handlers;

    public JobFactory(IEnumerable<IJobHandler> handlers)
    {
        _handlers = handlers
            .GroupBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    public IJobHandler? Resolve(string jobName) =>
        _handlers.TryGetValue(jobName, out var handler) ? handler : null;

    public IReadOnlyCollection<string> GetRegisteredJobNames() => _handlers.Keys.ToList();
}
