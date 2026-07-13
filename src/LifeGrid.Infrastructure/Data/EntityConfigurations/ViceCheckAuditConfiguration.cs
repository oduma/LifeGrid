using LifeGrid.Domain.ViceCheck;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeGrid.Infrastructure.Data.EntityConfigurations;

public sealed class ViceCheckAuditConfiguration : IEntityTypeConfiguration<ViceCheckAudit>
{
    public void Configure(EntityTypeBuilder<ViceCheckAudit> builder)
    {
        builder.ToTable("ViceCheckAudits");
        builder.HasKey(a => a.AuditId);
        builder.Property(a => a.AuditId).ValueGeneratedNever();
        builder.Property(a => a.WeekId);
        builder.Property(a => a.WeekGoalId);
        builder.Property(a => a.BadHabitId);
        builder.Property(a => a.GoalDescription).IsRequired().HasMaxLength(2000);
        builder.Property(a => a.BadHabitDescription).IsRequired().HasMaxLength(2000);
        builder.Property(a => a.DangerLevel);
        builder.Property(a => a.Question).IsRequired().HasMaxLength(1000);
        builder.Property(a => a.Answer).HasMaxLength(2000);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.PenaltyPercentApplied);
        builder.Property(a => a.CreatedAt);
        builder.Property(a => a.ResolvedAt);
        builder.HasIndex(a => a.WeekId);
        builder.HasIndex(a => a.BadHabitId);
    }
}
