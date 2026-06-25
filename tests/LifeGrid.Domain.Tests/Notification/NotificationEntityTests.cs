using FluentAssertions;
using NotificationEntity = LifeGrid.Domain.Notification.Notification;
using NotificationType   = LifeGrid.Domain.Notification.NotificationType;

namespace LifeGrid.Domain.Tests.Notification;

public sealed class NotificationEntityTests
{
    private static readonly DateTime Fixed = new(2026, 6, 25, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_SetsAllProperties_IsReadFalse()
    {
        var n = NotificationEntity.Create(
            "Shield Earned", "You have 1 shield.",
            NotificationType.ShieldUpdate,
            "lifegrid://habit/some-id", Fixed);

        n.NotificationId.Should().NotBeEmpty();
        n.Title.Should().Be("Shield Earned");
        n.Message.Should().Be("You have 1 shield.");
        n.Type.Should().Be(NotificationType.ShieldUpdate);
        n.DeepLinkUrl.Should().Be("lifegrid://habit/some-id");
        n.IsRead.Should().BeFalse();
        n.Timestamp.Should().Be(Fixed);
    }

    [Fact]
    public void Create_WithNullDeepLink_IsAllowed()
    {
        var n = NotificationEntity.Create("Title", "Msg", NotificationType.Warning, null, Fixed);

        n.DeepLinkUrl.Should().BeNull();
        n.IsRead.Should().BeFalse();
    }

    [Fact]
    public void MarkRead_SetsIsReadTrue()
    {
        var n = NotificationEntity.Create("T", "M", NotificationType.Quest, null, Fixed);
        n.IsRead.Should().BeFalse();

        n.MarkRead();

        n.IsRead.Should().BeTrue();
    }

    [Fact]
    public void Create_EachCall_ProducesUniqueNotificationId()
    {
        var a = NotificationEntity.Create("A", "A", NotificationType.Quest,       null, Fixed);
        var b = NotificationEntity.Create("B", "B", NotificationType.WeeklyRecap, null, Fixed);

        a.NotificationId.Should().NotBe(b.NotificationId);
    }
}
