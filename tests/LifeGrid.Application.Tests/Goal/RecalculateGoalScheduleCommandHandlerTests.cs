using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Habit;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using NSubstitute;
using GoalAggregate     = LifeGrid.Domain.Goal.Goal;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;
using WeekGoalEntity    = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.Tests.Goal;

public sealed class RecalculateGoalScheduleCommandHandlerTests
{
    private readonly IUserProfileRepository       _userProfiles = Substitute.For<IUserProfileRepository>();
    private readonly IGoalRepository              _goals        = Substitute.For<IGoalRepository>();
    private readonly IWeekRepository              _weekRepo     = Substitute.For<IWeekRepository>();
    private readonly IHabitRepository             _habitRepo    = Substitute.For<IHabitRepository>();
    private readonly IGeminiHabitGenerationService _aiService   = Substitute.For<IGeminiHabitGenerationService>();
    private readonly IUnitOfWork                  _uow          = Substitute.For<IUnitOfWork>();
    private readonly RecalculateGoalScheduleCommandHandler _handler;

    // 2026-01-05 is a Monday
    private static readonly DateTime MondayStart = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

    public RecalculateGoalScheduleCommandHandlerTests()
        => _handler = new RecalculateGoalScheduleCommandHandler(
            _userProfiles, _goals, _weekRepo, _habitRepo, _aiService, _uow);

    private static GoalAggregate BuildGoal(Guid userId, int durationDays = 100)
    {
        var goal = GoalAggregate.Create(
            userId, "Test goal", "#Test", $"{durationDays} days",
            MondayStart.AddDays(durationDays), MondayStart);
        goal.SetRefinementAnswers(new[] { (1, "Baseline question?", (string?)"My answer") });
        return goal;
    }

    private static readonly IReadOnlyList<WeekScheduleDto> SampleSchedule =
        new List<WeekScheduleDto>
        {
            new(1, MondayStart, new List<HabitScheduleItemDto>
            {
                new("Study 30 min", 30.0, "minutes")
            })
        };

    private void ArrangeBase(
        UserProfileEntity profile, GoalAggregate goal,
        HabitSchedulingResult? aiResult         = null,
        IReadOnlyList<WeekGoalEntity>? futureWgs = null,
        int maxWeekGoalNum                      = 0)
    {
        futureWgs ??= Array.Empty<WeekGoalEntity>();
        aiResult  ??= new HabitSchedulingResult.Feasible(SampleSchedule);

        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goals.GetByIdAsync(goal.GoalId, Arg.Any<CancellationToken>()).Returns(goal);
        _weekRepo.GetFutureWeekGoalsByGoalIdAsync(
                     goal.GoalId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns(futureWgs);
        _aiService.GenerateScheduleAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<HabitSchedulingResult>.Success(aiResult));
        _weekRepo.GetMaxWeekGoalNumberAsync(goal.GoalId, Arg.Any<CancellationToken>())
                 .Returns(maxWeekGoalNum);
    }

    [Fact]
    public async Task NoUserProfile_ReturnsFailure()
    {
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns((UserProfileEntity?)null);

        var result = await _handler.Handle(
            new RecalculateGoalScheduleCommand(Guid.NewGuid(), "I am overwhelmed"), default);

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

        var result = await _handler.Handle(
            new RecalculateGoalScheduleCommand(Guid.NewGuid(), "I am overwhelmed"), default);

        result.IsSuccess.Should().BeFalse();
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExtendDeadline_100DayGoal_AddsExactly25Days()
    {
        var profile          = UserProfileEntity.Create();
        var goal             = BuildGoal(profile.UserId, durationDays: 100);
        var originalDeadline = goal.DeadlineDate;
        ArrangeBase(profile, goal);

        await _handler.Handle(
            new RecalculateGoalScheduleCommand(goal.GoalId, "Too much"), default);

        goal.DeadlineDate.Should().Be(originalDeadline.AddDays(25));
    }

    [Fact]
    public async Task FlatPenalty_DeductsOnly100Xp_NotHistoricalXp()
    {
        var profile = UserProfileEntity.Create();
        profile.GrantXp(500);
        var goal = BuildGoal(profile.UserId);
        ArrangeBase(profile, goal);

        await _handler.Handle(
            new RecalculateGoalScheduleCommand(goal.GoalId, "Overwhelmed"), default);

        // Flat -100 XP only; historical XP records (WeekGoal.GoalWeeklyXpEarned) are untouched
        profile.Economy.LifetimeXp.Should().Be(400);
    }

    [Fact]
    public async Task GeminiInfeasible_ReturnsFailureWithoutCommit()
    {
        var profile = UserProfileEntity.Create();
        var goal    = BuildGoal(profile.UserId);
        ArrangeBase(profile, goal,
            aiResult: new HabitSchedulingResult.Infeasible("Too hard", null, null));

        var result = await _handler.Handle(
            new RecalculateGoalScheduleCommand(goal.GoalId, "Overwhelmed"), default);

        result.IsSuccess.Should().BeFalse();
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GeminiFeasible_InsertsNewWeekGoalsAndHabits()
    {
        var profile = UserProfileEntity.Create();
        var goal    = BuildGoal(profile.UserId);
        ArrangeBase(profile, goal);

        await _handler.Handle(
            new RecalculateGoalScheduleCommand(goal.GoalId, "Overwhelmed"), default);

        await _weekRepo.Received(1).AddAsync(
            Arg.Any<LifeGrid.Domain.Week.Week>(),
            Arg.Any<WeekGoalEntity>(),
            Arg.Any<CancellationToken>());
        await _habitRepo.Received(1).AddRangeAsync(
            Arg.Any<IReadOnlyList<LifeGrid.Domain.Habit.Habit>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GeminiFeasible_CommitsExactlyOnce()
    {
        var profile = UserProfileEntity.Create();
        var goal    = BuildGoal(profile.UserId);
        ArrangeBase(profile, goal);

        await _handler.Handle(
            new RecalculateGoalScheduleCommand(goal.GoalId, "Overwhelmed"), default);

        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WeekGoalNumber_ContinuesFromMaxExisting()
    {
        var profile = UserProfileEntity.Create();
        var goal    = BuildGoal(profile.UserId);
        ArrangeBase(profile, goal, maxWeekGoalNum: 3);

        WeekGoalEntity? capturedWeekGoal = null;
        await _weekRepo.AddAsync(
            Arg.Any<LifeGrid.Domain.Week.Week>(),
            Arg.Do<WeekGoalEntity>(wg => capturedWeekGoal = wg),
            Arg.Any<CancellationToken>());

        await _handler.Handle(
            new RecalculateGoalScheduleCommand(goal.GoalId, "Overwhelmed"), default);

        capturedWeekGoal.Should().NotBeNull();
        capturedWeekGoal!.WeekGoalNumber.Should().Be(4); // 3 + 1
    }
}
