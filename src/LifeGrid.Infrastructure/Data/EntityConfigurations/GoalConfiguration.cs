using LifeGrid.Domain.Goal;
using LifeGrid.Domain.UserProfile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeGrid.Infrastructure.Data.EntityConfigurations;

public sealed class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.ToTable("Goals");
        builder.HasKey(e => e.GoalId);
        builder.Property(e => e.GoalId).ValueGeneratedNever();
        builder.Property(e => e.UserId);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.AmbientTag).HasMaxLength(100);
        builder.Property(e => e.Duration).HasMaxLength(50);
        builder.Property(e => e.DeadlineDate);
        builder.Property(e => e.Status)
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.HasOne<UserProfile>()
               .WithMany()
               .HasForeignKey(e => e.UserId)
               .IsRequired()
               .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany(e => e.LinkedBadHabits, habit =>
        {
            habit.ToTable("GoalLinkedBadHabits");
            habit.WithOwner().HasForeignKey("GoalId");
            habit.HasKey(h => h.BadHabitId);
            habit.Property(h => h.BadHabitId).ValueGeneratedNever();
            habit.Property(h => h.Description).HasMaxLength(2000);
            habit.Property(h => h.DangerLevel);
        });
    }
}
