using FluentAssertions;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Domain.Tests.Vice;

public sealed class UserProfileViceSurveyTests
{
    [Fact]
    public void Create_IsViceSurveyCompleted_DefaultsFalse()
    {
        var profile = UserProfileEntity.Create();
        profile.IsViceSurveyCompleted.Should().BeFalse();
    }

    [Fact]
    public void GrantSurveyBonusShield_SetsIsViceSurveyCompleted()
    {
        var profile = UserProfileEntity.Create();
        profile.GrantSurveyBonusShield();
        profile.IsViceSurveyCompleted.Should().BeTrue();
    }

    [Fact]
    public void GrantSurveyBonusShield_ExpandsMaxCapToThree()
    {
        var profile = UserProfileEntity.Create();
        profile.GrantSurveyBonusShield();
        profile.Economy.MaxShieldCap.Should().Be(3);
    }

    [Fact]
    public void GrantSurveyBonusShield_GrantsOneShield()
    {
        var profile = UserProfileEntity.Create();
        profile.GrantSurveyBonusShield();
        profile.Economy.ShieldsAvailable.Should().Be(1);
    }

    [Fact]
    public void GrantSurveyBonusShield_Idempotent_DoesNotDoubleGrant()
    {
        var profile = UserProfileEntity.Create();
        profile.GrantSurveyBonusShield();
        profile.GrantSurveyBonusShield(); // second call is a no-op
        profile.Economy.ShieldsAvailable.Should().Be(1);
        profile.Economy.MaxShieldCap.Should().Be(3);
    }
}
