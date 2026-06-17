using LifeGrid.Domain.Goal;
using LifeGrid.Domain.Onboarding;
using LifeGrid.Domain.UserProfile;
using Microsoft.EntityFrameworkCore;

namespace LifeGrid.Infrastructure.Data;

public sealed class LifeGridDbContext(DbContextOptions<LifeGridDbContext> options)
    : DbContext(options)
{
    public DbSet<OnboardingSession> OnboardingSessions => Set<OnboardingSession>();
    public DbSet<UserProfile>       UserProfiles       => Set<UserProfile>();
    public DbSet<Goal>              Goals              => Set<Goal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(LifeGridDbContext).Assembly);
}
