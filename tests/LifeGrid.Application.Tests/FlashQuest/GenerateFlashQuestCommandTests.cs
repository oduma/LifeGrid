using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.FlashQuest;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Habit;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using NSubstitute;
using GoalEntity     = LifeGrid.Domain.Goal.Goal;
using HabitEntity    = LifeGrid.Domain.Habit.Habit;
using HabitType      = LifeGrid.Domain.Habit.HabitType;
using WeekEntity     = LifeGrid.Domain.Week.Week;
using WeekGoalEntity = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.Tests.FlashQuest;

public sealed class GenerateFlashQuestCommandTests
{
    private readonly IWeekRepository          _weekRepo  = Substitute.For<IWeekRepository>();
    private readonly IHabitRepository         _habitRepo = Substitute.For<IHabitRepository>();
    private readonly IGoalRepository          _goalRepo  = Substitute.For<IGoalRepository>();
    private readonly IGeminiFlashQuestService _gemini    = Substitute.For<IGeminiFlashQuestService>();
    private readonly IDateTimeProvider        _clock     = Substitute.For<IDateTimeProvider>();
    private readonly IUnitOfWork              _uow       = Substitute.For<IUnitOfWork>();

    private static readonly DateTime FixedNow =
        new(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc); // Thursday noon

    public GenerateFlashQuestCommandTests()
    {
        _clock.UtcNow.Returns(FixedNow);
        _habitRepo.HasFlashHabitsInWeekAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                  .Returns(false);
    }

    private GenerateFlashQuestCommandHandler BuildHandler() =>
        new(_weekRepo, _habitRepo, _goalRepo, _gemini, _clock, _uow);

    private static WeekEntity BuildWeekWithGoal(double goalWeeklyGp, out WeekGoalEntity weekGoal)
    {
        var week = WeekEntity.Create(1, new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc));
        weekGoal = WeekGoalEntity.Create(week.WeekId, Guid.NewGuid(), 1);
        weekGoal.SetGoalWeeklyGp(goalWeeklyGp);
        week.AddWeekGoal(weekGoal);
        return week;
    }

    [Fact]
    public async Task WeekNotFound_ReturnsFailure()
    {
        _weekRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                 .Returns((WeekEntity?)null);

        var result = await BuildHandler().Handle(new GenerateFlashQuestCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task AllGoalsAbove50Pct_NoOp_NoGeminiCall()
    {
        var week = BuildWeekWithGoal(75.0, out _);
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);

        var result = await BuildHandler().Handle(new GenerateFlashQuestCommand(week.WeekId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.QuestsInjected.Should().Be(0);
        await _gemini.DidNotReceive().GenerateAsync(
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AlreadyHasFlashHabits_SkipsPipelineEntirely()
    {
        var week = BuildWeekWithGoal(10.0, out _);
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _habitRepo.HasFlashHabitsInWeekAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await BuildHandler().Handle(new GenerateFlashQuestCommand(week.WeekId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.QuestsInjected.Should().Be(0);
        await _gemini.DidNotReceive().GenerateAsync(
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _habitRepo.DidNotReceive().AddRangeAsync(
            Arg.Any<IReadOnlyList<HabitEntity>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AiReturnsNotEligible_NoOp()
    {
        var week = BuildWeekWithGoal(45.0, out var weekGoal);
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);

        var habit   = HabitEntity.Create(
            weekGoal.WeekGoalId, HabitType.Planned, "Run", "Run daily", 10, "km",
            new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc));
        var habitId = habit.HabitId;
        _habitRepo.GetByWeekGoalIdAsync(weekGoal.WeekGoalId, Arg.Any<CancellationToken>())
                  .Returns(new List<HabitEntity> { habit });
        _habitRepo.GetCompletionSummariesForWeekGoalAsync(weekGoal.WeekGoalId, Arg.Any<CancellationToken>())
                  .Returns(new List<HabitCompletionSummaryDto>
                  {
                      new(habitId, 10, 2, HabitType.Planned)
                  });
        _goalRepo.GetByIdAsync(weekGoal.GoalId, Arg.Any<CancellationToken>())
                 .Returns(GoalEntity.Create(Guid.NewGuid(), "Get fit", "Fitness", "3 months",
                     new DateTime(2026, 9, 1), new DateTime(2026, 6, 22), new DateTime(2026, 6, 15)));
        _gemini.GenerateAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
               .Returns(Result<FlashQuestGenerationResult>.Success(
                   new FlashQuestGenerationResult.NotEligible()));

        var result = await BuildHandler().Handle(new GenerateFlashQuestCommand(week.WeekId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.QuestsInjected.Should().Be(0);
        await _habitRepo.DidNotReceive().AddRangeAsync(
            Arg.Any<IReadOnlyList<HabitEntity>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaggingGoalBelow50Pct_CallsGeminiAndInjectsFlashHabit()
    {
        var week = BuildWeekWithGoal(45.0, out var weekGoal);
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);

        var habit   = HabitEntity.Create(
            weekGoal.WeekGoalId, HabitType.Planned, "Run", "Run daily", 10, "km",
            new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc));
        var habitId = habit.HabitId;
        _habitRepo.GetByWeekGoalIdAsync(weekGoal.WeekGoalId, Arg.Any<CancellationToken>())
                  .Returns(new List<HabitEntity> { habit });
        _habitRepo.GetCompletionSummariesForWeekGoalAsync(weekGoal.WeekGoalId, Arg.Any<CancellationToken>())
                  .Returns(new List<HabitCompletionSummaryDto>
                  {
                      new(habitId, 10, 2, HabitType.Planned)
                  });
        _goalRepo.GetByIdAsync(weekGoal.GoalId, Arg.Any<CancellationToken>())
                 .Returns(GoalEntity.Create(Guid.NewGuid(), "Get fit", "Fitness", "3 months",
                     new DateTime(2026, 9, 1), new DateTime(2026, 6, 22), new DateTime(2026, 6, 15)));

        _gemini.GenerateAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
               .Returns(Result<FlashQuestGenerationResult>.Success(
                   new FlashQuestGenerationResult.Accepted(new List<FlashQuestItem>
                   {
                       new(habitId, "Quick Sprint", "Sprint for 20 minutes", 20, "minutes")
                   })));

        var result = await BuildHandler().Handle(new GenerateFlashQuestCommand(week.WeekId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.QuestsInjected.Should().Be(1);
        await _habitRepo.Received(1).AddRangeAsync(
            Arg.Is<IReadOnlyList<HabitEntity>>(list =>
                list.Count == 1
                && list[0].HabitType == HabitType.Flash
                && list[0].WeekGoalId == weekGoal.WeekGoalId
                && list[0].DeadlineDateTime == FixedNow.AddHours(24)),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AiReturnsUnknownSourceId_SkipsItem()
    {
        var week = BuildWeekWithGoal(45.0, out var weekGoal);
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);

        var habit   = HabitEntity.Create(
            weekGoal.WeekGoalId, HabitType.Planned, "Run", "Run daily", 10, "km",
            new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc));
        var habitId = habit.HabitId;
        _habitRepo.GetByWeekGoalIdAsync(weekGoal.WeekGoalId, Arg.Any<CancellationToken>())
                  .Returns(new List<HabitEntity> { habit });
        _habitRepo.GetCompletionSummariesForWeekGoalAsync(weekGoal.WeekGoalId, Arg.Any<CancellationToken>())
                  .Returns(new List<HabitCompletionSummaryDto>
                  {
                      new(habitId, 10, 2, HabitType.Planned)
                  });
        _goalRepo.GetByIdAsync(weekGoal.GoalId, Arg.Any<CancellationToken>())
                 .Returns(GoalEntity.Create(Guid.NewGuid(), "Get fit", "Fitness", "3 months",
                     new DateTime(2026, 9, 1), new DateTime(2026, 6, 22), new DateTime(2026, 6, 15)));

        _gemini.GenerateAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
               .Returns(Result<FlashQuestGenerationResult>.Success(
                   new FlashQuestGenerationResult.Accepted(new List<FlashQuestItem>
                   {
                       new(Guid.NewGuid(), "Bogus Quest", "Unknown source", 20, "minutes")
                   })));

        var result = await BuildHandler().Handle(new GenerateFlashQuestCommand(week.WeekId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.QuestsInjected.Should().Be(0);
        await _habitRepo.DidNotReceive().AddRangeAsync(
            Arg.Any<IReadOnlyList<HabitEntity>>(), Arg.Any<CancellationToken>());
    }
}
