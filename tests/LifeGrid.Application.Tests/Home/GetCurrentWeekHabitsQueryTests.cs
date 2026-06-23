using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Habit;
using LifeGrid.Application.Home;
using LifeGrid.Application.Week;
using NSubstitute;
using GoalAggregate = LifeGrid.Domain.Goal.Goal;
using HabitEntity  = LifeGrid.Domain.Habit.Habit;
using WeekEntity   = LifeGrid.Domain.Week.Week;

namespace LifeGrid.Application.Tests.Home;

public sealed class GetCurrentWeekHabitsQueryTests
{
    private readonly IWeekRepository                  _weekRepo  = Substitute.For<IWeekRepository>();
    private readonly IGoalRepository                  _goalRepo  = Substitute.For<IGoalRepository>();
    private readonly IHabitRepository                 _habitRepo = Substitute.For<IHabitRepository>();
    private readonly IDateTimeProvider                _clock     = Substitute.For<IDateTimeProvider>();
    private readonly GetCurrentWeekHabitsQueryHandler _handler;

    public GetCurrentWeekHabitsQueryTests()
        => _handler = new GetCurrentWeekHabitsQueryHandler(_weekRepo, _goalRepo, _habitRepo, _clock);

    // ── temporal resolution ───────────────────────────────────────────────────

    [Fact]
    public async Task WednesdayDate_QueriesForPrecedingMonday()
    {
        // 2026-06-24 is a Wednesday; 2026-06-22 is the preceding Monday
        _clock.UtcNow.Returns(new DateTime(2026, 6, 24, 12, 0, 0, DateTimeKind.Utc));
        _weekRepo.GetByStartDateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns((WeekEntity?)null);

        await _handler.Handle(new GetCurrentWeekHabitsQuery(), default);

        var expectedMonday = new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc);
        await _weekRepo.Received(1)
            .GetByStartDateAsync(
                Arg.Is<DateTime>(d => d.Date == expectedMonday.Date),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MondayDate_QueriesForSameDay()
    {
        // 2026-06-22 is a Monday — should query for the same date
        var monday = new DateTime(2026, 6, 22, 8, 0, 0, DateTimeKind.Utc);
        _clock.UtcNow.Returns(monday);
        _weekRepo.GetByStartDateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns((WeekEntity?)null);

        await _handler.Handle(new GetCurrentWeekHabitsQuery(), default);

        await _weekRepo.Received(1)
            .GetByStartDateAsync(
                Arg.Is<DateTime>(d => d.Date == monday.Date),
                Arg.Any<CancellationToken>());
    }

    // ── null week ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoWeekFound_ReturnsFailureResult()
    {
        _clock.UtcNow.Returns(new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc));
        _weekRepo.GetByStartDateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns((WeekEntity?)null);

        var result = await _handler.Handle(new GetCurrentWeekHabitsQuery(), default);

        result.IsSuccess.Should().BeFalse();
    }

    // ── empty week ────────────────────────────────────────────────────────────

    [Fact]
    public async Task WeekWithNoGoals_ReturnsEmptyGoalGroups()
    {
        _clock.UtcNow.Returns(new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc));
        var week = WeekEntity.Create(1, new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc));
        _weekRepo.GetByStartDateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns(week);
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(Array.Empty<GoalAggregate>());
        _habitRepo.GetByWeekGoalIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                  .Returns(Array.Empty<HabitEntity>());

        var result = await _handler.Handle(new GetCurrentWeekHabitsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.GoalGroups.Should().BeEmpty();
    }
}
