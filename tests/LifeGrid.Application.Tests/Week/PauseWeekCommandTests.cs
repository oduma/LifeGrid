using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.Notification;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Week;
using NSubstitute;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;
using WeekEntity        = LifeGrid.Domain.Week.Week;

namespace LifeGrid.Application.Tests.Week;

public sealed class PauseWeekCommandTests
{
    private readonly IWeekRepository          _weekRepo              = Substitute.For<IWeekRepository>();
    private readonly IUserProfileRepository   _profileRepo           = Substitute.For<IUserProfileRepository>();
    private readonly IDateTimeProvider        _clock                 = Substitute.For<IDateTimeProvider>();
    private readonly IUnitOfWork              _uow                   = Substitute.For<IUnitOfWork>();
    private readonly IEconomyStateBroadcaster _broadcaster           = Substitute.For<IEconomyStateBroadcaster>();
    private readonly INotificationRepository  _notificationRepo      = Substitute.For<INotificationRepository>();
    private readonly PauseWeekCommandHandler  _handler;

    // Fixed Monday two weeks from now — a future week
    private static readonly DateTime FutureMonday =
        new(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);

    // Fixed Monday in the past — current/past week
    private static readonly DateTime PastMonday =
        new(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc);

    // "Today" is Wednesday 2026-06-23 (before Friday, after past Monday)
    private static readonly DateTime Wednesday =
        new(2026, 6, 23, 0, 0, 0, DateTimeKind.Utc);

    // "Today" is Friday 2026-06-27 — outside the freeze window
    private static readonly DateTime Friday =
        new(2026, 6, 27, 0, 0, 0, DateTimeKind.Utc);

    public PauseWeekCommandTests()
    {
        _clock.UtcNow.Returns(Wednesday);
        _handler = new PauseWeekCommandHandler(
            _weekRepo, _profileRepo, _clock, _uow, _broadcaster, _notificationRepo);
    }

    // ── Hibernate ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Hibernate_FutureWeek_Succeeds()
    {
        var week = WeekEntity.Create(10, FutureMonday);
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);

        var result = await _handler.Handle(
            new PauseWeekCommand(week.WeekId, WeekStatus.Hibernated), default);

        result.IsSuccess.Should().BeTrue();
        week.Status.Should().Be(WeekStatus.Hibernated);
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        _broadcaster.Received(1).Broadcast();
    }

    [Fact]
    public async Task Hibernate_AlreadyStartedWeek_ReturnsWeekAlreadyStartedFailure()
    {
        var week = WeekEntity.Create(5, PastMonday);
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);

        var result = await _handler.Handle(
            new PauseWeekCommand(week.WeekId, WeekStatus.Hibernated), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("week_already_started");
        week.Status.Should().Be(WeekStatus.Active);
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    // ── Freeze ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Freeze_CurrentWeek_BeforeFriday_HasShields_Succeeds_ConsumesOneShield()
    {
        var week    = WeekEntity.Create(5, PastMonday);
        var profile = UserProfileEntity.Create();
        profile.GrantSp(30); // grants 1 shield
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _weekRepo.GetByWeekNumberAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                 .Returns((WeekEntity?)null);

        var shieldsBefore = profile.Economy.ShieldsAvailable;
        var result = await _handler.Handle(
            new PauseWeekCommand(week.WeekId, WeekStatus.Frozen), default);

        result.IsSuccess.Should().BeTrue();
        week.Status.Should().Be(WeekStatus.Frozen);
        profile.Economy.ShieldsAvailable.Should().Be(shieldsBefore - 1);
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        _broadcaster.Received(1).BroadcastEconomy(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Freeze_NoShields_ReturnsNoShieldsFailure()
    {
        var week    = WeekEntity.Create(5, PastMonday);
        var profile = UserProfileEntity.Create(); // 0 shields
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _handler.Handle(
            new PauseWeekCommand(week.WeekId, WeekStatus.Frozen), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("no_shields");
        week.Status.Should().Be(WeekStatus.Active);
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Freeze_AfterThursday_ReturnsWindowClosedFailure()
    {
        _clock.UtcNow.Returns(Friday);
        var week    = WeekEntity.Create(5, PastMonday);
        var profile = UserProfileEntity.Create();
        profile.GrantSp(30);
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _handler.Handle(
            new PauseWeekCommand(week.WeekId, WeekStatus.Frozen), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("freeze_window_closed");
        week.Status.Should().Be(WeekStatus.Active);
    }

    // ── Re-entry week detection ────────────────────────────────────────────────

    [Fact]
    public async Task Freeze_PreviousWeekWasFrozen_MarksNextWeekAsReEntry()
    {
        var prevWeek = WeekEntity.Create(4, PastMonday.AddDays(-7));
        prevWeek.Pause(WeekStatus.Frozen);

        var week     = WeekEntity.Create(5, PastMonday);
        var nextWeek = WeekEntity.Create(6, PastMonday.AddDays(7));

        var profile = UserProfileEntity.Create();
        profile.GrantSp(30);

        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _weekRepo.GetByWeekNumberAsync(4, Arg.Any<CancellationToken>()).Returns(prevWeek);
        _weekRepo.GetByWeekNumberAsync(6, Arg.Any<CancellationToken>()).Returns(nextWeek);

        await _handler.Handle(new PauseWeekCommand(week.WeekId, WeekStatus.Frozen), default);

        nextWeek.IsReEntryWeek.Should().BeTrue();
    }

    [Fact]
    public async Task Freeze_PreviousWeekNotFrozen_DoesNotMarkNextAsReEntry()
    {
        var prevWeek = WeekEntity.Create(4, PastMonday.AddDays(-7));
        // prevWeek status remains Active (not frozen)

        var week     = WeekEntity.Create(5, PastMonday);
        var nextWeek = WeekEntity.Create(6, PastMonday.AddDays(7));

        var profile = UserProfileEntity.Create();
        profile.GrantSp(30);

        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _weekRepo.GetByWeekNumberAsync(4, Arg.Any<CancellationToken>()).Returns(prevWeek);
        _weekRepo.GetByWeekNumberAsync(6, Arg.Any<CancellationToken>()).Returns(nextWeek);

        await _handler.Handle(new PauseWeekCommand(week.WeekId, WeekStatus.Frozen), default);

        nextWeek.IsReEntryWeek.Should().BeFalse();
    }
}
