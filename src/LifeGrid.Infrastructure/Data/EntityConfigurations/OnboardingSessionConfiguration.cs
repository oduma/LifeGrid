using LifeGrid.Domain.Onboarding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeGrid.Infrastructure.Data.EntityConfigurations;

public sealed class OnboardingSessionConfiguration : IEntityTypeConfiguration<OnboardingSession>
{
    public void Configure(EntityTypeBuilder<OnboardingSession> builder)
    {
        builder.ToTable("OnboardingProgressCache");
        builder.HasKey(e => e.SessionId);
        builder.Property(e => e.SessionId).ValueGeneratedNever();
        builder.Property(e => e.CurrentStep)
               .HasConversion<string>()
               .HasMaxLength(64);
        builder.Property(e => e.RawGoalDraft).HasMaxLength(2000);
        builder.Property(e => e.IsComplete);
        builder.Property(e => e.LastActiveTimestamp);
    }
}
