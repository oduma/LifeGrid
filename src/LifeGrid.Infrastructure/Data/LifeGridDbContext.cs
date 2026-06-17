using LifeGrid.Domain.Onboarding;
using Microsoft.EntityFrameworkCore;

namespace LifeGrid.Infrastructure.Data;

public sealed class LifeGridDbContext(DbContextOptions<LifeGridDbContext> options)
    : DbContext(options)
{
    public DbSet<OnboardingSession> OnboardingSessions => Set<OnboardingSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(LifeGridDbContext).Assembly);
}
