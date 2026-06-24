using LifeGrid.Domain.Badge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeGrid.Infrastructure.Data.EntityConfigurations;

public sealed class BadgeConfiguration : IEntityTypeConfiguration<Badge>
{
    public void Configure(EntityTypeBuilder<Badge> builder)
    {
        builder.ToTable("Badges");
        builder.HasKey(b => b.BadgeId);
        builder.Property(b => b.BadgeId).ValueGeneratedNever();
        builder.Property(b => b.UserId);
        builder.Property(b => b.GoalId);
        builder.Property(b => b.WeekId);
        builder.Property(b => b.BadgeType).HasMaxLength(100);
        builder.Property(b => b.BadgeName).HasMaxLength(200);
        builder.Property(b => b.Description).HasMaxLength(500);
        builder.Property(b => b.IconName).HasMaxLength(200);
        builder.Property(b => b.Tier).HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.IsEarned);
        builder.Property(b => b.DateEarned);
    }
}
