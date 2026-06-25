using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.Habit;
using LifeGrid.Application.HabitLogging;
using LifeGrid.Application.Notification;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Week;
using NSubstitute;
using CompletedValueLog = LifeGrid.Domain.Habit.CompletedValueLog;
using HabitEntity       = LifeGrid.Domain.Habit.Habit;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;
using WeekEntity        = LifeGrid.Domain.Week.Week;
using WeekGoalEntity    = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.Tests.HabitLogging;

public sealed class LogHabitProgressCommandTests
{
    private readonly IHabitRepository         _habitRepo        = Substitute.For<IHabitRepository>();
    private readonly IWeekRepository          _weekRepo         = Substitute.For<IWeekRepository>();
    private readonly IUserProfileRepository   _profileRepo      = Substitute.For<IUserProfileRepository>();
    private readonly IDateTimeProvider        _clock            = Substitute.For<IDateTimeProvider>();
    private readonly IUnitOfWork              _uow              = Substitute.For<IUnitOfWork>();
    private readonly IEconomyStateBroadcaster _broadcaster      = Substitute.For<IEconomyStateBroadcaster>();
    private readonly INotificationRepository  _notificationRepo = Substitute.For<INotificationRepository>();
    private readonly LogHabitProgressCommandHandler _handler;

    private static readonly DateTime FixedNow =
        new(2026, 6, 23, 10, 30, 0, DateTimeKind.Utc);

    private static readonly WeekGoalEntity SeedWeekGoal =
        WeekGoalEntity.Create(Guid.NewGuid(), Guid.NewGuid(), 1);

    private static readonly WeekEntity SeedWeek =
        WeekEntity.Create(1, new DateTime(2026, 6, 23, 0, 0, 0, DateTimeKind.Utc));

    private static readonly UserProfileEntity SeedProfile =
        UserProfileEntity.Create();

    public LogHabitProgressCommandTests()
    {
        _clock.UtcNow.Returns(FixedNow);

        // Default happy-path stubs for new dependencies
        _weekRepo.GetWeekGoalByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                 .Returns(SeedWeekGoal);
        _weekRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                 .Returns(SeedWeek);
        _weekRepo.GetWeekGoalGpStatsAsync(Arg.Any<CancellationToken>())
                 .Returns((0.0, 1));
        _habitRepo.GetCompletionSummariesForWeekGoalAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                  .Returns(Array.Empty<HabitCompletionSummaryDto>());
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>())
                    .Returns(SeedProfile);

        _handler = new LogHabitProgressCommandHandler(
            _habitRepo, _weekRepo, _profileRepo, _clock, _uow, _broadcaster, _notificationRepo);
    }

    // ── validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValueZero_ReturnsFailure()
    {
        var result = await _handler.Handle(
            new LogHabitProgressCommand(Guid.NewGuid(), 0, "km", null, null), default);

        result.IsSuccess.Should().BeFalse();
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NegativeValue_ReturnsFailure()
    {
        var result = await _handler.Handle(
            new LogHabitProgressCommand(Guid.NewGuid(), -1.5, "km", null, null), default);

        result.IsSuccess.Should().BeFalse();
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    // ── habit not found ───────────────────────────────────────────────────────

    [Fact]
    public async Task HabitNotFound_ReturnsFailure()
    {
        _habitRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                  .Returns((HabitEntity?)null);

        var result = await _handler.Handle(
            new LogHabitProgressCommand(Guid.NewGuid(), 5.0, "km", null, null), default);

        result.IsSuccess.Should().BeFalse();
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task HappyPath_AddsLogAndCommits()
    {
        var habitId = Guid.NewGuid();
        var habit   = HabitEntity.Create(
            Guid.NewGuid(),
            LifeGrid.Domain.Habit.HabitType.Planned,
            "Run 5k", "Run five kilometres",
            5.0, "km",
            new DateTime(2026, 6, 27, 0, 0, 0, DateTimeKind.Utc));

        _habitRepo.GetByIdAsync(habitId, Arg.Any<CancellationToken>()).Returns(habit);

        var result = await _handler.Handle(
            new LogHabitProgressCommand(habitId, 5.0, "km", "Felt great!", null), default);

        result.IsSuccess.Should().BeTrue();
        await _habitRepo.Received(1)
            .AddCompletionLogAsync(
                Arg.Is<CompletedValueLog>(l => l.HabitId == habitId),
                Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HappyPath_UsesDateTimeProviderTimestamp()
    {
        var habitId = Guid.NewGuid();
        var habit   = HabitEntity.Create(
            Guid.NewGuid(),
            LifeGrid.Domain.Habit.HabitType.Planned,
            "Meditate", "10 minutes of meditation",
            10.0, "min",
            new DateTime(2026, 6, 27, 0, 0, 0, DateTimeKind.Utc));

        _habitRepo.GetByIdAsync(habitId, Arg.Any<CancellationToken>()).Returns(habit);

        await _handler.Handle(
            new LogHabitProgressCommand(habitId, 10.0, "min", null, null), default);

        await _habitRepo.Received(1)
            .AddCompletionLogAsync(
                Arg.Is<CompletedValueLog>(l => l.Timestamp == FixedNow),
                Arg.Any<CancellationToken>());
    }

    // ── gamification ──────────────────────────────────────────────────────────

    [Fact]
    public async Task HappyPath_CallsBroadcasterExactlyOnce()
    {
        var habitId = Guid.NewGuid();
        var habit   = HabitEntity.Create(
            Guid.NewGuid(), LifeGrid.Domain.Habit.HabitType.Planned,
            "Run 5k", "Run five kilometres", 5.0, "km",
            new DateTime(2026, 6, 27, 0, 0, 0, DateTimeKind.Utc));

        _habitRepo.GetByIdAsync(habitId, Arg.Any<CancellationToken>()).Returns(habit);

        var result = await _handler.Handle(
            new LogHabitProgressCommand(habitId, 5.0, "km", null, null), default);

        result.IsSuccess.Should().BeTrue();
        _broadcaster.Received(1).BroadcastEconomy(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task HappyPath_AppliesXpToProfile()
    {
        var habitId = Guid.NewGuid();
        var habit   = HabitEntity.Create(
            Guid.NewGuid(), LifeGrid.Domain.Habit.HabitType.Planned,
            "Run 5k", "Run five kilometres", 5.0, "km",
            new DateTime(2026, 6, 27, 0, 0, 0, DateTimeKind.Utc));

        _habitRepo.GetByIdAsync(habitId, Arg.Any<CancellationToken>()).Returns(habit);

        // Profile starts at LifetimeXp = 0; after handling it should be > 0
        var result = await _handler.Handle(
            new LogHabitProgressCommand(habitId, 5.0, "km", null, null), default);

        result.IsSuccess.Should().BeTrue();
        SeedProfile.Economy.LifetimeXp.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task HappyPath_RecordsGpOnWeekGoal()
    {
        var habitId    = Guid.NewGuid();
        var weekGoalId = Guid.NewGuid();
        var habit      = HabitEntity.Create(
            weekGoalId, LifeGrid.Domain.Habit.HabitType.Planned,
            "Run 5k", "Run five kilometres", 5.0, "km",
            new DateTime(2026, 6, 27, 0, 0, 0, DateTimeKind.Utc));

        var weekGoal = WeekGoalEntity.Create(Guid.NewGuid(), Guid.NewGuid(), 1);

        _habitRepo.GetByIdAsync(habitId, Arg.Any<CancellationToken>()).Returns(habit);
        _weekRepo.GetWeekGoalByIdAsync(weekGoalId, Arg.Any<CancellationToken>()).Returns(weekGoal);
        _habitRepo.GetCompletionSummariesForWeekGoalAsync(weekGoalId, Arg.Any<CancellationToken>())
            .Returns(new List<HabitCompletionSummaryDto>
            {
                new(habitId, 5.0, 0.0, LifeGrid.Domain.Habit.HabitType.Planned)
            });

        var result = await _handler.Handle(
            new LogHabitProgressCommand(habitId, 5.0, "km", null, null), default);

        result.IsSuccess.Should().BeTrue();
        weekGoal.GoalWeeklyGp.Should().BeGreaterThan(0);
    }

    // ── re-entry week scaling ─────────────────────────────────────────────────

    [Fact]
    public async Task ReEntryWeek_EffectiveTargetReducedToSeventyPercent()
    {
        // Arrange: habit target = 10.0; re-entry effective target = ceil(10 * 0.7) = 7.
        // Logging 7.0 on a normal week gives < 100% GP.
        // Logging 7.0 on a re-entry week should give 100% GP (exact effective target met).
        const double storedTarget = 10.0;
        const double loggedValue  = 7.0;

        var habitId    = Guid.NewGuid();
        var weekGoalId = Guid.NewGuid();
        var habit      = HabitEntity.Create(
            weekGoalId, LifeGrid.Domain.Habit.HabitType.Planned,
            "Run 7k", "Reduced target for re-entry", storedTarget, "km",
            new DateTime(2026, 6, 27, 0, 0, 0, DateTimeKind.Utc));

        var reEntryWeek = WeekEntity.Create(2, new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc));
        reEntryWeek.MarkAsReEntry();

        var weekGoal = WeekGoalEntity.Create(reEntryWeek.WeekId, Guid.NewGuid(), 1);

        _habitRepo.GetByIdAsync(habitId, Arg.Any<CancellationToken>()).Returns(habit);
        _weekRepo.GetWeekGoalByIdAsync(weekGoalId, Arg.Any<CancellationToken>()).Returns(weekGoal);
        _weekRepo.GetByIdAsync(reEntryWeek.WeekId, Arg.Any<CancellationToken>()).Returns(reEntryWeek);
        _habitRepo.GetCompletionSummariesForWeekGoalAsync(weekGoalId, Arg.Any<CancellationToken>())
            .Returns(new List<HabitCompletionSummaryDto>
            {
                new(habitId, storedTarget, 0.0, LifeGrid.Domain.Habit.HabitType.Planned)
            });

        await _handler.Handle(
            new LogHabitProgressCommand(habitId, loggedValue, "km", null, null), default);

        // With effectiveTarget = 7.0 and totalActual = 7.0, GP should be 100
        weekGoal.GoalWeeklyGp.Should().BeApproximately(100.0, precision: 0.1);
    }

    // ── atomic consistency ────────────────────────────────────────────────────

    [Fact]
    public async Task CommitFailure_BroadcasterNotCalled()
    {
        var habitId = Guid.NewGuid();
        var habit   = HabitEntity.Create(
            Guid.NewGuid(), LifeGrid.Domain.Habit.HabitType.Planned,
            "Run 5k", "Run five kilometres", 5.0, "km",
            new DateTime(2026, 6, 27, 0, 0, 0, DateTimeKind.Utc));

        _habitRepo.GetByIdAsync(habitId, Arg.Any<CancellationToken>()).Returns(habit);
        _uow.CommitAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("DB failure")));

        var act = async () => await _handler.Handle(
            new LogHabitProgressCommand(habitId, 5.0, "km", null, null), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _broadcaster.DidNotReceive().BroadcastEconomy(Arg.Any<int>(), Arg.Any<int>());
    }
}
