using FluentAssertions;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Domain.Tests.UserProfile;

public sealed class UserProfileTests
{
    [Fact]
    public void Create_GeneratesNonEmptyGuid()
    {
        var profile = UserProfileEntity.Create();
        profile.UserId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_TwoProfiles_HaveDifferentIds()
    {
        var a = UserProfileEntity.Create();
        var b = UserProfileEntity.Create();
        a.UserId.Should().NotBe(b.UserId);
    }

    [Fact]
    public void Create_SetsLevelToOne()
    {
        var profile = UserProfileEntity.Create();
        profile.CurrentLevel.Should().Be(1);
    }

    [Fact]
    public void Create_Economy_AllDefaultsAreZero()
    {
        var profile = UserProfileEntity.Create();
        profile.Economy.LifetimeXp.Should().Be(0);
        profile.Economy.CurrentSp.Should().Be(0);
        profile.Economy.ShieldsAvailable.Should().Be(0);
        profile.Economy.LifetimeGpAverage.Should().Be(0.0);
    }

    [Fact]
    public void Create_Economy_MaxShieldCapIsTwo()
    {
        var profile = UserProfileEntity.Create();
        profile.Economy.MaxShieldCap.Should().Be(2);
    }

    [Fact]
    public void Create_ActiveStates_DoubleXpModeIsFalse()
    {
        var profile = UserProfileEntity.Create();
        profile.ActiveStates.DoubleXpMode.Should().BeFalse();
    }

    [Fact]
    public void Create_HasEmptyBadgesCollection()
    {
        var profile = UserProfileEntity.Create();
        profile.Badges.Should().BeEmpty();
    }
}
