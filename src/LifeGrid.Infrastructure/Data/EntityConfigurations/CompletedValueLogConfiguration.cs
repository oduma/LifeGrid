using LifeGrid.Domain.Habit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeGrid.Infrastructure.Data.EntityConfigurations;

internal sealed class CompletedValueLogConfiguration : IEntityTypeConfiguration<CompletedValueLog>
{
    public void Configure(EntityTypeBuilder<CompletedValueLog> builder)
    {
        builder.ToTable("HabitCompletionLogs");
        builder.HasKey(l => l.LogId);

        builder.Property(l => l.HabitId);
        builder.Property(l => l.ActualValue);
        builder.Property(l => l.MeasurementUnit).HasMaxLength(100);
        builder.Property(l => l.ProofText).HasMaxLength(2000);
        builder.Property(l => l.ProofImageUrl).HasMaxLength(1000);
        builder.Property(l => l.Timestamp);
    }
}
