using LifeGrid.Domain.UserProfile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeGrid.Infrastructure.Data.EntityConfigurations;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");
        builder.HasKey(e => e.UserId);
        builder.Property(e => e.UserId).ValueGeneratedNever();
        builder.Property(e => e.CurrentLevel);
        builder.Property(e => e.IsViceSurveyCompleted);

        builder.OwnsOne(e => e.Economy, economy =>
        {
            economy.ToTable("UserEconomy");
            economy.Property(e => e.LifetimeGpAverage);
            economy.Property(e => e.LifetimeXp);
            economy.Property(e => e.CurrentSp);
            economy.Property(e => e.ShieldsAvailable);
            economy.Property(e => e.MaxShieldCap);
        });

        builder.OwnsOne(e => e.ActiveStates, states =>
        {
            states.ToTable("UserActiveStates");
            states.Property(e => e.DoubleXpMode);
            states.Property(e => e.DoubleXpExpiry);
        });

        builder.OwnsMany(e => e.Badges, badge =>
        {
            badge.ToTable("UserBadges");
            badge.WithOwner().HasForeignKey("UserId");
            badge.HasKey(b => b.BadgeId);
            badge.Property(b => b.BadgeId).ValueGeneratedNever();
            badge.Property(b => b.BadgeType).HasMaxLength(100);
            badge.Property(b => b.Description).HasMaxLength(2000);
            badge.Property(b => b.DateEarned);
        });
    }
}
