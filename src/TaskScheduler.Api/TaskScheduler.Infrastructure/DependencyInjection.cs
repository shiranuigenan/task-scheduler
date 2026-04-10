using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskScheduler.Application.Jobs;
using TaskScheduler.Application.Services;
using TaskScheduler.Infrastructure.Persistence;
using TaskScheduler.Infrastructure.Scheduling;

namespace TaskScheduler.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=taskscheduler;Username=postgres;Password=q";

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddSingleton<GroupLockManager>();
        services.AddScoped<ScheduledTaskRepository>();
        services.AddSingleton<JobFactory>();
        services.AddSingleton<IJobHandler, SendEmailJobHandler>();
        services.AddSingleton<IJobHandler, CleanupJobHandler>();
        services.AddScoped<ScheduledTaskService>();
        services.AddHostedService<TaskSchedulerBackgroundService>();

        return services;
    }
}
