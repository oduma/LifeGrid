using LifeGrid.Domain.Habit;
using LifeGrid.Domain.WeekGoal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeGrid.Infrastructure.Data.EntityConfigurations;

internal sealed class HabitConfiguration : IEntityTypeConfiguration<Habit>
{
    public void Configure(EntityTypeBuilder<Habit> builder)
    {
        builder.ToTable("Habits");
        builder.HasKey(h => h.HabitId);

        builder.Property(h => h.WeekGoalId);
        builder.Property(h => h.HabitName).HasMaxLength(500);
        builder.Property(h => h.HabitDescription).HasMaxLength(2000);
        builder.Property(h => h.TargetValue);
        builder.Property(h => h.MeasurementUnit).HasMaxLength(100);
        builder.Property(h => h.DeadlineDateTime);
        builder.Property(h => h.HabitType)
               .HasConversion<string>()
               .HasMaxLength(30);

        // FK to WeekGoal — configured from Habit side since WeekGoal has no Habits navigation
        builder.HasOne<WeekGoal>()
               .WithMany()
               .HasForeignKey(h => h.WeekGoalId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
