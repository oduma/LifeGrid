using FluentAssertions;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Timeline;
using LifeGrid.Application.Week;
using NSubstitute;
using GoalAggregate     = LifeGrid.Domain.Goal.Goal;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;
using WeekEntity        = LifeGrid.Domain.Week.Week;
using WeekGoalEntity    = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.Tests.Timeline;

public sealed class GetTimelineQueryFilterTests
{
    private readonly IWeekRepository       _weekRepo = Substitute.For<IWeekRepository>();
    private readonly IGoalRepository       _goalRepo = Substitute.For<IGoalRepository>();
    private readonly GetTimelineQueryHandler _handler;

    public GetTimelineQueryFilterTests()
        => _handler = new GetTimelineQueryHandler(_weekRepo, _goalRepo);

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

    private static WeekEntity MakeWeek(int n, params (WeekEntity week, Guid goalId, int goalNum)[] goals)
    {
        var w = WeekEntity.Create(n, new DateTime(2026, 6, n, 0, 0, 0, DateTimeKind.Utc));
        foreach (var (_, goalId, goalNum) in goals)
            w.AddWeekGoal(WeekGoalEntity.Create(w.WeekId, goalId, goalNum));
        return w;
    }

    // ── null / empty filter ───────────────────────────────────────────────────

    [Fact]
    public async Task NullFilter_ReturnsAllWeeks()
    {
        var goalA = MakeGoal("A");
        var goalB = MakeGoal("B");
        var week  = WeekEntity.Create(1, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        week.AddWeekGoal(WeekGoalEntity.Create(week.WeekId, goalA.GoalId, 1));
        week.AddWeekGoal(WeekGoalEntity.Create(week.WeekId, goalB.GoalId, 2));

        _weekRepo.GetTimelineAsync(Arg.Any<CancellationToken>()).Returns(new[] { week });
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(new[] { goalA, goalB });

        var result = await _handler.Handle(new GetTimelineQuery(null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value![0].Goals.Should().HaveCount(2);
    }

    [Fact]
    public async Task EmptyFilter_ReturnsAllWeeks()
    {
        var goal = MakeGoal("A");
        var week = WeekEntity.Create(1, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        week.AddWeekGoal(WeekGoalEntity.Create(week.WeekId, goal.GoalId, 1));

        _weekRepo.GetTimelineAsync(Arg.Any<CancellationToken>()).Returns(new[] { week });
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(new[] { goal });

        var result = await _handler.Handle(new GetTimelineQuery(new List<Guid>()), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value![0].Goals.Should().HaveCount(1);
    }

    // ── single-goal filter ────────────────────────────────────────────────────

    [Fact]
    public async Task SingleGoalFilter_ExcludesNonMatchingGoalItems()
    {
        var goalA = MakeGoal("A");
        var goalB = MakeGoal("B");
        var week  = WeekEntity.Create(1, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        week.AddWeekGoal(WeekGoalEntity.Create(week.WeekId, goalA.GoalId, 1));
        week.AddWeekGoal(WeekGoalEntity.Create(week.WeekId, goalB.GoalId, 2));

        _weekRepo.GetTimelineAsync(Arg.Any<CancellationToken>()).Returns(new[] { week });
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(new[] { goalA });

        var result = await _handler.Handle(new GetTimelineQuery(new[] { goalA.GoalId }), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value![0].Goals.Should().HaveCount(1);
        result.Value![0].Goals[0].GoalDescription.Should().Be("A");
    }

    [Fact]
    public async Task SingleGoalFilter_ExcludesWeeksWithZeroMatchingItems()
    {
        var goalA = MakeGoal("A");
        var goalB = MakeGoal("B");

        var weekOnlyB = WeekEntity.Create(1, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        weekOnlyB.AddWeekGoal(WeekGoalEntity.Create(weekOnlyB.WeekId, goalB.GoalId, 1));

        _weekRepo.GetTimelineAsync(Arg.Any<CancellationToken>()).Returns(new[] { weekOnlyB });
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(Array.Empty<GoalAggregate>());

        var result = await _handler.Handle(new GetTimelineQuery(new[] { goalA.GoalId }), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    // ── multi-goal filter ─────────────────────────────────────────────────────

    [Fact]
    public async Task MultiGoalFilter_IncludesWeeksMatchingAnyGoal()
    {
        var goalA = MakeGoal("A");
        var goalB = MakeGoal("B");
        var goalC = MakeGoal("C");

        // week1 has goalA + goalB; after filter [goalA, goalC] → should keep only goalA item
        var week1 = WeekEntity.Create(1, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        week1.AddWeekGoal(WeekGoalEntity.Create(week1.WeekId, goalA.GoalId, 1));
        week1.AddWeekGoal(WeekGoalEntity.Create(week1.WeekId, goalB.GoalId, 2));

        // week2 has goalC; filter [goalA, goalC] → should include week2 with goalC
        var week2 = WeekEntity.Create(2, new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc));
        week2.AddWeekGoal(WeekGoalEntity.Create(week2.WeekId, goalC.GoalId, 1));

        _weekRepo.GetTimelineAsync(Arg.Any<CancellationToken>()).Returns(new[] { week1, week2 });
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(new[] { goalA, goalC });

        var result = await _handler.Handle(
            new GetTimelineQuery(new[] { goalA.GoalId, goalC.GoalId }), default);

        result.IsSuccess.Should().BeTrue();
        var dtos = result.Value!.ToList();
        dtos.Should().HaveCount(2);
        dtos[0].Goals.Should().HaveCount(1).And.Contain(g => g.GoalDescription == "A");
        dtos[1].Goals.Should().HaveCount(1).And.Contain(g => g.GoalDescription == "C");
    }
}
