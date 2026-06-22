using FluentAssertions;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Timeline;
using LifeGrid.Application.Week;
using NSubstitute;
using GoalAggregate       = LifeGrid.Domain.Goal.Goal;
using UserProfileEntity   = LifeGrid.Domain.UserProfile.UserProfile;
using WeekEntity          = LifeGrid.Domain.Week.Week;
using WeekGoalEntity      = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.Tests.Week;

public sealed class GetTimelineQueryHandlerTests
{
    private readonly IWeekRepository  _weekRepo  = Substitute.For<IWeekRepository>();
    private readonly IGoalRepository  _goalRepo  = Substitute.For<IGoalRepository>();
    private readonly GetTimelineQueryHandler _handler;

    public GetTimelineQueryHandlerTests()
        => _handler = new GetTimelineQueryHandler(_weekRepo, _goalRepo);

    private static GoalAggregate MakeGoal(string description = "Run a marathon")
    {
        var profile = UserProfileEntity.Create();
        return GoalAggregate.Create(
            profile.UserId, description, "#Fitness", "6 months",
            new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task NoWeeks_ReturnsEmptyList()
    {
        _weekRepo.GetTimelineAsync(Arg.Any<CancellationToken>())
                 .Returns(Array.Empty<WeekEntity>());

        var result = await _handler.Handle(new GetTimelineQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task WeekGoalItem_ResolvesGoalDescription_FromGoalId()
    {
        var goal = MakeGoal("Learn Spanish");
        var week = WeekEntity.Create(1, new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc));
        var wg   = WeekGoalEntity.Create(week.WeekId, goal.GoalId, 1);
        week.AddWeekGoal(wg);

        _weekRepo.GetTimelineAsync(Arg.Any<CancellationToken>())
                 .Returns(new[] { week });
        _goalRepo.GetByIdsAsync(
                     Arg.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == goal.GoalId),
                     Arg.Any<CancellationToken>())
                 .Returns(new[] { goal });

        var result = await _handler.Handle(new GetTimelineQuery(), default);

        result.IsSuccess.Should().BeTrue();
        var weekDto = result.Value!.Single();
        weekDto.Goals.Single().GoalDescription.Should().Be("Learn Spanish");
    }

    [Fact]
    public async Task MultipleWeeks_PreservesRepoOrder()
    {
        var weekA = WeekEntity.Create(1, new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc));
        var weekB = WeekEntity.Create(2, new DateTime(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc));

        _weekRepo.GetTimelineAsync(Arg.Any<CancellationToken>())
                 .Returns(new[] { weekA, weekB });
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(Array.Empty<GoalAggregate>());

        var result = await _handler.Handle(new GetTimelineQuery(), default);

        result.IsSuccess.Should().BeTrue();
        var dtos = result.Value!.ToList();
        dtos[0].StartDate.Should().Be(weekA.StartDate);
        dtos[1].StartDate.Should().Be(weekB.StartDate);
    }

    [Fact]
    public async Task WeekGoalItems_BelongToCorrectWeek_NoDataBleed()
    {
        var goalA = MakeGoal("Goal A");
        var goalB = MakeGoal("Goal B");

        var weekA = WeekEntity.Create(1, new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc));
        var wgA   = WeekGoalEntity.Create(weekA.WeekId, goalA.GoalId, 1);
        weekA.AddWeekGoal(wgA);

        var weekB = WeekEntity.Create(2, new DateTime(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc));
        var wgB   = WeekGoalEntity.Create(weekB.WeekId, goalB.GoalId, 1);
        weekB.AddWeekGoal(wgB);

        _weekRepo.GetTimelineAsync(Arg.Any<CancellationToken>())
                 .Returns(new[] { weekA, weekB });
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(new[] { goalA, goalB });

        var result = await _handler.Handle(new GetTimelineQuery(), default);

        result.IsSuccess.Should().BeTrue();
        var dtos = result.Value!.ToList();
        dtos[0].Goals.Should().HaveCount(1).And.Contain(g => g.GoalDescription == "Goal A");
        dtos[1].Goals.Should().HaveCount(1).And.Contain(g => g.GoalDescription == "Goal B");
    }

    [Fact]
    public async Task AllMetricFields_MappedCorrectly()
    {
        var goal = MakeGoal("Test Goal");
        var week = WeekEntity.Create(1, new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc));
        var wg   = WeekGoalEntity.Create(week.WeekId, goal.GoalId, 1);
        wg.SetGoalWeeklyGp(3.75);
        wg.SetGoalWeeklyXpEarned(120);
        week.AddWeekGoal(wg);

        _weekRepo.GetTimelineAsync(Arg.Any<CancellationToken>())
                 .Returns(new[] { week });
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(new[] { goal });

        var result = await _handler.Handle(new GetTimelineQuery(), default);

        result.IsSuccess.Should().BeTrue();
        var goalDto = result.Value!.Single().Goals.Single();
        goalDto.PenaltyState.Should().Be("Clean");
        goalDto.GoalWeeklyGp.Should().Be(3.75);
        goalDto.GoalWeeklyXpEarned.Should().Be(120);
    }
}
