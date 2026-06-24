using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Infrastructure.Data.EntityConfigurations;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfileEntity>
{
    public void Configure(EntityTypeBuilder<UserProfileEntity> builder)
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

    }
}
