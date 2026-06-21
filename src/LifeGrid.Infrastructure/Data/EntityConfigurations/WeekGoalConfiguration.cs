using LifeGrid.Domain.WeekGoal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeGrid.Infrastructure.Data.EntityConfigurations;

internal sealed class WeekGoalConfiguration : IEntityTypeConfiguration<WeekGoal>
{
    public void Configure(EntityTypeBuilder<WeekGoal> builder)
    {
        builder.ToTable("WeekGoals");
        builder.HasKey(wg => wg.WeekGoalId);

        builder.Property(wg => wg.WeekId);
        builder.Property(wg => wg.GoalId);
        builder.Property(wg => wg.WeekGoalNumber);
        builder.Property(wg => wg.GoalWeeklyGp);
        builder.Property(wg => wg.GoalWeeklyXpEarned);
        builder.Property(wg => wg.PenaltyState)
               .HasConversion<string>()
               .HasMaxLength(30);
    }
}
