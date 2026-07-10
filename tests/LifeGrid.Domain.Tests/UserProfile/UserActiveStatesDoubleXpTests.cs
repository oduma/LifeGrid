using FluentAssertions;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Domain.Tests.UserProfile;

public sealed class UserActiveStatesDoubleXpTests
{
    [Fact]
    public void ActivateDoubleXp_SetsModeAndExpiry()
    {
        var profile = UserProfileEntity.Create();
        var expiry  = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);

        profile.ActivateDoubleXp(expiry);

        profile.ActiveStates.DoubleXpMode.Should().BeTrue();
        profile.ActiveStates.DoubleXpExpiry.Should().Be(expiry);
    }

    [Fact]
    public void IsDoubleXpActive_BeforeExpiry_ReturnsTrue()
    {
        var profile = UserProfileEntity.Create();
        var expiry  = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        profile.ActivateDoubleXp(expiry);

        profile.IsDoubleXpActive(expiry.AddMinutes(-1)).Should().BeTrue();
    }

    [Fact]
    public void IsDoubleXpActive_AfterExpiry_ReturnsFalse()
    {
        var profile = UserProfileEntity.Create();
        var expiry  = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        profile.ActivateDoubleXp(expiry);

        profile.IsDoubleXpActive(expiry.AddMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void IsDoubleXpActive_NeverActivated_ReturnsFalse()
    {
        var profile = UserProfileEntity.Create();

        profile.IsDoubleXpActive(DateTime.UtcNow).Should().BeFalse();
    }
}
