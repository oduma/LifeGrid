using LifeGrid.Application.Common;
using LifeGrid.Domain.Badge;
using LifeGrid.Domain.Goal;
using LifeGrid.Domain.Habit;
using LifeGrid.Domain.Notification;
using LifeGrid.Domain.Onboarding;
using LifeGrid.Domain.UserProfile;
using LifeGrid.Domain.Week;
using LifeGrid.Domain.WeekGoal;
using Microsoft.EntityFrameworkCore;

namespace LifeGrid.Infrastructure.Data;

public sealed class LifeGridDbContext(DbContextOptions<LifeGridDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<OnboardingSession> OnboardingSessions  => Set<OnboardingSession>();
    public DbSet<UserProfile>       UserProfiles        => Set<UserProfile>();
    public DbSet<Badge>             Badges              => Set<Badge>();
    public DbSet<LoginHistory>      LoginHistory        => Set<LoginHistory>();
    public DbSet<Goal>              Goals               => Set<Goal>();
    public DbSet<Week>              Weeks               => Set<Week>();
    public DbSet<WeekGoal>          WeekGoals           => Set<WeekGoal>();
    public DbSet<Habit>             Habits              => Set<Habit>();
    public DbSet<CompletedValueLog> CompletedValueLogs  => Set<CompletedValueLog>();
    public DbSet<Notification>      Notifications       => Set<Notification>();

    public Task CommitAsync(CancellationToken ct = default) => SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(LifeGridDbContext).Assembly);
}
