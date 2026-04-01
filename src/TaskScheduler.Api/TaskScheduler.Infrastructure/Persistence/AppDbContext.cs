using Microsoft.EntityFrameworkCore;
using TaskScheduler.Domain.Entities;

namespace TaskScheduler.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ScheduledTask> ScheduledTasks => Set<ScheduledTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ScheduledTask>();
        entity.ToTable("ScheduledTasks");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.JobName).HasMaxLength(128).IsRequired();
        entity.Property(x => x.ParametersJson).IsRequired();
        entity.Property(x => x.GroupKey).HasMaxLength(256);
    }
}
