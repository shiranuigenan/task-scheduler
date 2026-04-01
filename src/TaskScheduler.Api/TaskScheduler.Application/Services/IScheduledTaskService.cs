using TaskScheduler.Application.Dtos;

namespace TaskScheduler.Application.Services;

public interface IScheduledTaskService
{
    Task<IReadOnlyList<TaskResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);
    Task ActivateAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
