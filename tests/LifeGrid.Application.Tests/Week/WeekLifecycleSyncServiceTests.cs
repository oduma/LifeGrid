using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Notification;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.Week;
using MediatR;
using NSubstitute;
using WeekEntity       = LifeGrid.Domain.Week.Week;
using NotificationEntity = LifeGrid.Domain.Notification.Notification;

namespace LifeGrid.Application.Tests.Week;

public sealed class WeekLifecycleSyncServiceTests
{
    private readonly IWeekRepository         _weekRepo    = Substitute.For<IWeekRepository>();
    private readonly INotificationRepository _notifRepo   = Substitute.For<INotificationRepository>();
    private readonly IUnitOfWork             _uow         = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider       _clock       = Substitute.For<IDateTimeProvider>();
    private readonly ISender                 _sender      = Substitute.For<ISender>();
    private readonly IPushNotificationService _push       = Substitute.For<IPushNotificationService>();

    private static readonly DateTime PreviousMonday = new(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc);

    private WeekLifecycleSyncService BuildService() =>
        new(_weekRepo, _notifRepo, _uow, _clock, _sender, _push);

    // ── Wednesday auto-close ──────────────────────────────────────────────────

    [Fact]
    public async Task Wednesday_AutoCloses_UnclosedWeek_AndAddsNotification_AndPushes()
    {
        var wednesday = new DateTime(2026, 7, 1, 9, 1, 0, DateTimeKind.Utc);
        _clock.UtcNow.Returns(wednesday);

        var week = WeekEntity.Create(5, PreviousMonday);
        _weekRepo.GetByStartDateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns(week);
        _sender.Send(Arg.Any<CloseWeekCommand>(), Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(Result.Success()));

        await BuildService().EvaluateAsync();

        await _sender.Received(1).Send(
            Arg.Is<CloseWeekCommand>(c => c.WeekId == week.WeekId),
            Arg.Any<CancellationToken>());
        await _notifRepo.Received(1).AddAsync(
            Arg.Is<NotificationEntity>(n => n.Title == "Week Auto-Closed"),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _push.Received(1).SendAsync(
            Arg.Is<string>(s => s == "Week Auto-Closed"),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ── Monday reminder ───────────────────────────────────────────────────────

    [Fact]
    public async Task Monday_Sends_ReminderNotification_AndPushes_WithoutClosing()
    {
        var monday = new DateTime(2026, 6, 29, 9, 0, 0, DateTimeKind.Utc);
        _clock.UtcNow.Returns(monday);

        var week = WeekEntity.Create(5, PreviousMonday);
        _weekRepo.GetByStartDateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns(week);

        await BuildService().EvaluateAsync();

        await _sender.DidNotReceive().Send(
            Arg.Any<CloseWeekCommand>(), Arg.Any<CancellationToken>());
        await _notifRepo.Received(1).AddAsync(
            Arg.Is<NotificationEntity>(n => n.Title == "Week Ended"),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _push.Received(1).SendAsync(
            Arg.Is<string>(s => s == "Week Ended"),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ── Already closed ────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_WeekAlreadyClosed_DoesNothing()
    {
        var wednesday = new DateTime(2026, 7, 1, 9, 1, 0, DateTimeKind.Utc);
        _clock.UtcNow.Returns(wednesday);

        var week = WeekEntity.Create(5, PreviousMonday);
        week.Close();
        _weekRepo.GetByStartDateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns(week);

        await BuildService().EvaluateAsync();

        await _sender.DidNotReceive().Send(Arg.Any<IRequest<Result>>(), Arg.Any<CancellationToken>());
        await _notifRepo.DidNotReceive().AddAsync(Arg.Any<NotificationEntity>(), Arg.Any<CancellationToken>());
        await _push.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ── GetPreviousWeekMonday helper ──────────────────────────────────────────

    [Theory]
    [InlineData(2026, 6, 29, 2026, 6, 22)] // Monday June 29 → prev Monday June 22
    [InlineData(2026, 7,  1, 2026, 6, 22)] // Wednesday July 1 → prev Monday June 22
    [InlineData(2026, 6, 28, 2026, 6, 22)] // Sunday June 28 → prev Monday June 22
    public void GetPreviousWeekMonday_ReturnsCorrectDate(
        int todayYear, int todayMonth, int todayDay,
        int expectedYear, int expectedMonth, int expectedDay)
    {
        var today    = new DateTime(todayYear, todayMonth, todayDay, 0, 0, 0, DateTimeKind.Utc);
        var expected = new DateTime(expectedYear, expectedMonth, expectedDay, 0, 0, 0, DateTimeKind.Utc);

        var result = WeekLifecycleSyncService.GetPreviousWeekMonday(today);

        result.Should().Be(expected);
    }
}
