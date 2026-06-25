using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Notification;
using LifeGrid.Domain.Notification;
using NSubstitute;
using NotificationEntity = LifeGrid.Domain.Notification.Notification;

namespace LifeGrid.Application.Tests.Notification;

public sealed class GetNotificationsQueryTests
{
    private readonly INotificationRepository       _repo    = Substitute.For<INotificationRepository>();
    private readonly GetNotificationsQueryHandler  _handler;

    private static readonly DateTime Fixed = new(2026, 6, 25, 10, 0, 0, DateTimeKind.Utc);

    public GetNotificationsQueryTests()
    {
        _handler = new GetNotificationsQueryHandler(_repo);
    }

    [Fact]
    public async Task Handle_ReturnsAllNotifications_MappedToDto()
    {
        var n1 = NotificationEntity.Create("Shield Earned", "1 shield.", NotificationType.ShieldUpdate, null, Fixed);
        var n2 = NotificationEntity.Create("Quest", "Do it!", NotificationType.Quest, "lifegrid://habit/abc", Fixed.AddHours(-1));
        _repo.GetAllAsync(Arg.Any<CancellationToken>())
             .Returns(new List<NotificationEntity> { n1, n2 });

        var result = await _handler.Handle(new GetNotificationsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value![0].TypeLabel.Should().Be("SHIELD");
        result.Value[1].DeepLinkUrl.Should().Be("lifegrid://habit/abc");
    }

    [Fact]
    public async Task Handle_EmptyRepository_ReturnsEmptyList()
    {
        _repo.GetAllAsync(Arg.Any<CancellationToken>())
             .Returns(Array.Empty<NotificationEntity>());

        var result = await _handler.Handle(new GetNotificationsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
