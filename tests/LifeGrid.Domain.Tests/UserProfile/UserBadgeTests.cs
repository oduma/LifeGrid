using FluentAssertions;
using LifeGrid.Domain.UserProfile;

namespace LifeGrid.Domain.Tests.UserProfile;

public sealed class UserBadgeTests
{
    private static readonly DateTime Fixed = new(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_SetsAllProperties()
    {
        var badge = UserBadge.Create("Showing_Up_Gold", "Logged a habit 7 days in a row.", "", Fixed);

        badge.BadgeId.Should().NotBe(Guid.Empty);
        badge.BadgeType.Should().Be("Showing_Up_Gold");
        badge.Description.Should().Be("Logged a habit 7 days in a row.");
        badge.IconName.Should().Be("");
        badge.DateEarned.Should().Be(Fixed);
    }
}
