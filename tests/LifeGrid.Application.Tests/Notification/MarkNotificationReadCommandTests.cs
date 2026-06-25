using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Notification;
using LifeGrid.Domain.Notification;
using NSubstitute;
using NotificationEntity = LifeGrid.Domain.Notification.Notification;

namespace LifeGrid.Application.Tests.Notification;

public sealed class MarkNotificationReadCommandTests
{
    private readonly INotificationRepository        _repo    = Substitute.For<INotificationRepository>();
    private readonly IUnitOfWork                    _uow     = Substitute.For<IUnitOfWork>();
    private readonly MarkNotificationReadCommandHandler _handler;

    private static readonly DateTime Fixed = new(2026, 6, 25, 10, 0, 0, DateTimeKind.Utc);

    public MarkNotificationReadCommandTests()
    {
        _handler = new MarkNotificationReadCommandHandler(_repo, _uow);
    }

    [Fact]
    public async Task Handle_ValidId_MarksReadAndCommits()
    {
        var n = NotificationEntity.Create("T", "M", NotificationType.Warning, null, Fixed);
        _repo.GetByIdAsync(n.NotificationId, Arg.Any<CancellationToken>()).Returns(n);

        var result = await _handler.Handle(
            new MarkNotificationReadCommand(n.NotificationId), default);

        result.IsSuccess.Should().BeTrue();
        n.IsRead.Should().BeTrue();
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownId_ReturnsFailure_NoCommit()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns((NotificationEntity?)null);

        var result = await _handler.Handle(
            new MarkNotificationReadCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("notification_not_found");
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MarkAsRead_DoesNotModifyOriginalMessageOrTimestamp()
    {
        const string originalMessage = "Original message text.";
        var n = NotificationEntity.Create("T", originalMessage, NotificationType.Quest, null, Fixed);
        _repo.GetByIdAsync(n.NotificationId, Arg.Any<CancellationToken>()).Returns(n);

        await _handler.Handle(new MarkNotificationReadCommand(n.NotificationId), default);

        n.Message.Should().Be(originalMessage);
        n.Timestamp.Should().Be(Fixed);
    }
}
