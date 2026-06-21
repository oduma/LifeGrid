using FluentAssertions;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Domain.Tests.UserProfile;

public sealed class UserEconomyDeductXpTests
{
    [Fact]
    public void GrantXp_IncreasesLifetimeXp()
    {
        var profile = UserProfileEntity.Create();

        profile.GrantXp(450);

        profile.Economy.LifetimeXp.Should().Be(450);
    }

    [Fact]
    public void DeductXp_NormalAmount_ReducesLifetimeXp()
    {
        var profile = UserProfileEntity.Create();
        profile.GrantXp(500);

        profile.DeductXp(200);

        profile.Economy.LifetimeXp.Should().Be(300);
    }

    [Fact]
    public void DeductXp_ExceedsBalance_FloorsAtZero()
    {
        var profile = UserProfileEntity.Create();
        profile.GrantXp(100);

        profile.DeductXp(300);

        profile.Economy.LifetimeXp.Should().Be(0);
    }

    [Fact]
    public void DeductXp_ZeroAmount_NoChange()
    {
        var profile = UserProfileEntity.Create();
        profile.GrantXp(500);

        profile.DeductXp(0);

        profile.Economy.LifetimeXp.Should().Be(500);
    }

    [Fact]
    public void DeductXp_ExactBalance_GoesToZero()
    {
        var profile = UserProfileEntity.Create();
        profile.GrantXp(550);

        profile.DeductXp(550);

        profile.Economy.LifetimeXp.Should().Be(0);
    }
}
