using FluentAssertions;
using LifeGrid.Domain.Badge;

namespace LifeGrid.Domain.Tests.Badge;

public sealed class LoginHistoryTests
{
    [Fact]
    public void Create_SetsUserIdAndTimestamp()
    {
        var userId    = Guid.NewGuid();
        var timestamp = new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc);

        var entry = LoginHistory.Create(userId, timestamp);

        entry.Id.Should().NotBe(Guid.Empty);
        entry.UserId.Should().Be(userId);
        entry.Timestamp.Should().Be(timestamp);
    }
}
