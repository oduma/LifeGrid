using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Habit;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Week;
using NSubstitute;
using GoalAggregate     = LifeGrid.Domain.Goal.Goal;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;
using WeekGoalEntity    = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.Tests.Goal;

public sealed class AbandonGoalCommandHandlerTests
{
    private readonly IUserProfileRepository _userProfiles = Substitute.For<IUserProfileRepository>();
    private readonly IGoalRepository        _goals        = Substitute.For<IGoalRepository>();
    private readonly IWeekRepository        _weekRepo     = Substitute.For<IWeekRepository>();
    private readonly IHabitRepository       _habitRepo    = Substitute.For<IHabitRepository>();
    private readonly IUnitOfWork            _uow          = Substitute.For<IUnitOfWork>();
    private readonly AbandonGoalCommandHandler _handler;

    private static readonly DateTime MondayStart = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

    public AbandonGoalCommandHandlerTests()
        => _handler = new AbandonGoalCommandHandler(
            _userProfiles, _goals, _weekRepo, _habitRepo, _uow);

    private static GoalAggregate SampleGoal(Guid userId)
        => GoalAggregate.Create(userId, "Run a marathon", "#Fitness", "6 months",
            MondayStart.AddDays(180), MondayStart);

    private void ArrangeBase(UserProfileEntity profile, GoalAggregate goal, int historicalXp = 0,
        IReadOnlyList<WeekGoalEntity>? futureWeekGoals = null)
    {
        futureWeekGoals ??= Array.Empty<WeekGoalEntity>();

        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goals.GetByIdAsync(goal.GoalId, Arg.Any<CancellationToken>()).Returns(goal);
        _weekRepo.GetHistoricalXpByGoalIdAsync(goal.GoalId, Arg.Any<CancellationToken>())
                 .Returns(historicalXp);
        _weekRepo.GetFutureWeekGoalsByGoalIdAsync(
                     goal.GoalId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns(futureWeekGoals);
    }

    [Fact]
    public async Task NoUserProfile_ReturnsFailure()
    {
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns((UserProfileEntity?)null);

        var result = await _handler.Handle(new AbandonGoalCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GoalNotFound_ReturnsFailure()
    {
        var profile = UserProfileEntity.Create();
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goals.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
              .Returns((GoalAggregate?)null);

        var result = await _handler.Handle(new AbandonGoalCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AbandonGoal_SetsStatusAbandoned()
    {
        var profile = UserProfileEntity.Create();
        var goal    = SampleGoal(profile.UserId);
        ArrangeBase(profile, goal);

        await _handler.Handle(new AbandonGoalCommand(goal.GoalId), default);

        goal.Status.Should().Be(LifeGrid.Domain.Goal.GoalStatus.Abandoned);
    }

    [Fact]
    public async Task AbandonGoal_450XpEarned_Deducts550FromLifetimeXp()
    {
        var profile = UserProfileEntity.Create();
        profile.GrantXp(1000);
        var goal = SampleGoal(profile.UserId);
        ArrangeBase(profile, goal, historicalXp: 450);

        var result = await _handler.Handle(new AbandonGoalCommand(goal.GoalId), default);

        result.IsSuccess.Should().BeTrue();
        profile.Economy.LifetimeXp.Should().Be(450); // 1000 - 550
    }

    [Fact]
    public async Task AbandonGoal_PenaltyExceedsBalance_FloorsAtZero()
    {
        var profile = UserProfileEntity.Create();
        profile.GrantXp(300);
        var goal = SampleGoal(profile.UserId);
        ArrangeBase(profile, goal, historicalXp: 450); // penalty = 550, balance = 300 → 0

        var result = await _handler.Handle(new AbandonGoalCommand(goal.GoalId), default);

        result.IsSuccess.Should().BeTrue();
        profile.Economy.LifetimeXp.Should().Be(0);
    }

    [Fact]
    public async Task AbandonGoal_DeletesFutureWeekGoalsAndHabits()
    {
        var profile        = UserProfileEntity.Create();
        var goal           = SampleGoal(profile.UserId);
        var futureWeekGoal = WeekGoalEntity.Create(Guid.NewGuid(), goal.GoalId, 5);
        ArrangeBase(profile, goal, futureWeekGoals: new[] { futureWeekGoal });

        await _handler.Handle(new AbandonGoalCommand(goal.GoalId), default);

        await _habitRepo.Received(1).RemoveByWeekGoalIdsAsync(
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == futureWeekGoal.WeekGoalId),
            Arg.Any<CancellationToken>());
        await _weekRepo.Received(1).RemoveWeekGoalRangeAsync(
            Arg.Is<IReadOnlyList<WeekGoalEntity>>(wgs => wgs.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AbandonGoal_CommitsExactlyOnce()
    {
        var profile = UserProfileEntity.Create();
        var goal    = SampleGoal(profile.UserId);
        ArrangeBase(profile, goal);

        await _handler.Handle(new AbandonGoalCommand(goal.GoalId), default);

        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
