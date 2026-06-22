using LifeGrid.Domain.Onboarding;
using LifeGrid.Domain.UserProfile;
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
        builder.Property(e => e.LastActiveTimestamp);
        builder.Property(e => e.UserId);
        builder.HasOne<UserProfile>()
               .WithMany()
               .HasForeignKey(e => e.UserId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.SetNull);
        builder.Property(e => e.ValidatedGoalJson);
        builder.Property(e => e.RefinementQuestionsJson);
        builder.Property(e => e.RefinementAnswersJson);
        builder.Property(e => e.ChosenStartDate);
        builder.Property(e => e.BlueprintJson);
        builder.Property(e => e.GoalId);
    }
}
