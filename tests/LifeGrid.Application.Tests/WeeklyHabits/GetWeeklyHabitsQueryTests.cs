using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Habit;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.ViceCheck;
using LifeGrid.Application.Week;
using LifeGrid.Application.WeeklyHabits;
using NSubstitute;
using GoalAggregate     = LifeGrid.Domain.Goal.Goal;
using HabitEntity       = LifeGrid.Domain.Habit.Habit;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;
using WeekEntity        = LifeGrid.Domain.Week.Week;
using WeekGoalEntity    = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.Tests.WeeklyHabits;

public sealed class GetWeeklyHabitsQueryTests
{
    private readonly IWeekRepository           _weekRepo    = Substitute.For<IWeekRepository>();
    private readonly IGoalRepository           _goalRepo    = Substitute.For<IGoalRepository>();
    private readonly IHabitRepository          _habitRepo   = Substitute.For<IHabitRepository>();
    private readonly IUserProfileRepository    _profileRepo = Substitute.For<IUserProfileRepository>();
    private readonly IViceCheckAuditRepository _auditRepo   = Substitute.For<IViceCheckAuditRepository>();
    private readonly IDateTimeProvider         _clock       = Substitute.For<IDateTimeProvider>();
    private readonly GetWeeklyHabitsQueryHandler _handler;

    public GetWeeklyHabitsQueryTests()
    {
        _auditRepo.HasAuditForWeekAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        _handler = new GetWeeklyHabitsQueryHandler(
            _weekRepo, _goalRepo, _habitRepo, _profileRepo, _auditRepo, _clock);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static GoalAggregate MakeGoal(string description = "Goal")
    {
        var profile = UserProfileEntity.Create();
        return GoalAggregate.Create(
            profile.UserId, description, "#Test", "6 months",
            new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22,  0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 16,  0, 0, 0, DateTimeKind.Utc));
    }

    private static HabitEntity MakeHabit(Guid weekGoalId)
        => HabitEntity.Create(
            weekGoalId,
            LifeGrid.Domain.Habit.HabitType.Planned,
            "Run 5k", "Run five kilometres",
            5.0, "km",
            new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc));

    // ── null filter ───────────────────────────────────────────────────────────

    [Fact]
    public async Task NullFilter_ReturnsAllGoalGroups()
    {
        var goalA = MakeGoal("A");
        var goalB = MakeGoal("B");
        var week  = WeekEntity.Create(1, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var wgA   = WeekGoalEntity.Create(week.WeekId, goalA.GoalId, 1);
        var wgB   = WeekGoalEntity.Create(week.WeekId, goalB.GoalId, 2);
        week.AddWeekGoal(wgA);
        week.AddWeekGoal(wgB);

        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(new[] { goalA, goalB });
        _habitRepo.GetByWeekGoalIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                  .Returns(Array.Empty<HabitEntity>());

        var result = await _handler.Handle(new GetWeeklyHabitsQuery(week.WeekId, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.GoalGroups.Should().HaveCount(2);
    }

    // ── filtered ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task FilteredGoalIds_ExcludesNonMatchingGoalGroups()
    {
        var goalA = MakeGoal("A");
        var goalB = MakeGoal("B");
        var week  = WeekEntity.Create(1, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var wgA   = WeekGoalEntity.Create(week.WeekId, goalA.GoalId, 1);
        var wgB   = WeekGoalEntity.Create(week.WeekId, goalB.GoalId, 2);
        week.AddWeekGoal(wgA);
        week.AddWeekGoal(wgB);

        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(new[] { goalA });
        _habitRepo.GetByWeekGoalIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                  .Returns(Array.Empty<HabitEntity>());

        var result = await _handler.Handle(
            new GetWeeklyHabitsQuery(week.WeekId, new[] { goalA.GoalId }), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.GoalGroups.Should().HaveCount(1);
        result.Value!.GoalGroups[0].GoalDescription.Should().Be("A");
    }

    // ── habit grouping ────────────────────────────────────────────────────────

    [Fact]
    public async Task HabitsGroupedUnderCorrectWeekGoal()
    {
        var goalA = MakeGoal("A");
        var goalB = MakeGoal("B");
        var week  = WeekEntity.Create(1, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var wgA   = WeekGoalEntity.Create(week.WeekId, goalA.GoalId, 1);
        var wgB   = WeekGoalEntity.Create(week.WeekId, goalB.GoalId, 2);
        week.AddWeekGoal(wgA);
        week.AddWeekGoal(wgB);

        var habitA = MakeHabit(wgA.WeekGoalId);
        var habitB1 = MakeHabit(wgB.WeekGoalId);
        var habitB2 = MakeHabit(wgB.WeekGoalId);

        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(new[] { goalA, goalB });
        _habitRepo.GetByWeekGoalIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                  .Returns(new[] { habitA, habitB1, habitB2 });

        var result = await _handler.Handle(new GetWeeklyHabitsQuery(week.WeekId, null), default);

        result.IsSuccess.Should().BeTrue();
        var groups = result.Value!.GoalGroups;
        groups.Should().HaveCount(2);
        groups.First(g => g.GoalDescription == "A").Habits.Should().HaveCount(1);
        groups.First(g => g.GoalDescription == "B").Habits.Should().HaveCount(2);
    }

    // ── vice check availability ───────────────────────────────────────────────

    [Fact]
    public async Task IsViceCheckAvailable_True_WhenAllGatesPass()
    {
        var week = WeekEntity.Create(1, new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        week.Close();
        _clock.UtcNow.Returns(new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc)); // within 72h window

        var profile = UserProfileEntity.Create();
        profile.GrantSurveyBonusShield(); // sets IsViceSurveyCompleted = true

        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(Array.Empty<GoalAggregate>());
        _habitRepo.GetByWeekGoalIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                  .Returns(Array.Empty<HabitEntity>());
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _handler.Handle(new GetWeeklyHabitsQuery(week.WeekId, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsViceCheckAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task IsViceCheckAvailable_False_WhenAlreadyAudited()
    {
        var week = WeekEntity.Create(1, new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        week.Close();
        _clock.UtcNow.Returns(new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc));

        var profile = UserProfileEntity.Create();
        profile.GrantSurveyBonusShield();

        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(Array.Empty<GoalAggregate>());
        _habitRepo.GetByWeekGoalIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                  .Returns(Array.Empty<HabitEntity>());
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _auditRepo.HasAuditForWeekAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(new GetWeeklyHabitsQuery(week.WeekId, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsViceCheckAvailable.Should().BeFalse();
    }
}
