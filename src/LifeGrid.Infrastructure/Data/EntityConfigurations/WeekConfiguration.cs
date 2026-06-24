using LifeGrid.Domain.Week;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeGrid.Infrastructure.Data.EntityConfigurations;

internal sealed class WeekConfiguration : IEntityTypeConfiguration<Week>
{
    public void Configure(EntityTypeBuilder<Week> builder)
    {
        builder.ToTable("Weeks");
        builder.HasKey(w => w.WeekId);

        builder.Property(w => w.WeekNumber);
        builder.Property(w => w.StartDate);
        builder.Property(w => w.TotalWeeklySpEarned);
        builder.Property(w => w.Status)
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(w => w.IsReEntryWeek);

        builder.HasMany(w => w.WeekGoals)
               .WithOne()
               .HasForeignKey(wg => wg.WeekId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
