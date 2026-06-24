using FluentAssertions;
using LifeGrid.Domain.Badge;
using BadgeEntity = LifeGrid.Domain.Badge.Badge;

namespace LifeGrid.Domain.Tests.Badge;

public sealed class BadgeTests
{
    private static readonly DateTime Fixed = new(2026, 6, 24, 23, 59, 59, DateTimeKind.Utc);

    [Fact]
    public void CreateEarned_SetsAllProperties()
    {
        var badge = BadgeEntity.CreateEarned(
            Guid.NewGuid(), "Showing_Up_Bronze", "Mr. Consistency (Bronze)",
            "Logged in every day. Achieved: 24 Jun 2026",
            "", BadgeTier.Bronze, Fixed);

        badge.BadgeId.Should().NotBe(Guid.Empty);
        badge.BadgeType.Should().Be("Showing_Up_Bronze");
        badge.BadgeName.Should().Be("Mr. Consistency (Bronze)");
        badge.Description.Should().Be("Logged in every day. Achieved: 24 Jun 2026");
        badge.Tier.Should().Be(BadgeTier.Bronze);
        badge.DateEarned.Should().Be(Fixed);
    }

    [Fact]
    public void CreateEarned_IsEarned_IsTrue()
    {
        var badge = BadgeEntity.CreateEarned(
            Guid.NewGuid(), "Showing_Up_Gold", "Mr. Consistency (Gold)",
            "desc", "", BadgeTier.Gold, Fixed);

        badge.IsEarned.Should().BeTrue();
    }
}
