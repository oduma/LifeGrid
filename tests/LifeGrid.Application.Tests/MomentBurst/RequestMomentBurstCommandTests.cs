using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Habit;
using LifeGrid.Application.MomentBurst;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.Habit;
using NSubstitute;
using WeekEntity     = LifeGrid.Domain.Week.Week;
using WeekGoalEntity = LifeGrid.Domain.WeekGoal.WeekGoal;
using HabitEntity    = LifeGrid.Domain.Habit.Habit;

namespace LifeGrid.Application.Tests.MomentBurst;

public sealed class RequestMomentBurstCommandTests
{
    private readonly IWeekRepository           _weekRepo    = Substitute.For<IWeekRepository>();
    private readonly IHabitRepository          _habitRepo   = Substitute.For<IHabitRepository>();
    private readonly IGeminiMomentBurstService _aiService   = Substitute.For<IGeminiMomentBurstService>();
    private readonly IDateTimeProvider         _clock       = Substitute.For<IDateTimeProvider>();
    private readonly IUnitOfWork               _uow         = Substitute.For<IUnitOfWork>();

    private static readonly Guid   WeekGoalId = Guid.NewGuid();
    private static readonly Guid   GoalId     = Guid.NewGuid();

    // June 24, 2026 = Wednesday; currentMonday = June 22
    private static readonly DateTime Today         = new(2026, 6, 24, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CurrentMonday = new(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc);

    private WeekGoalEntity MakeWeekGoal(double gp = 100.0)
    {
        var wg = WeekGoalEntity.Create(Guid.NewGuid(), GoalId, 1);
        if (gp > 0) wg.RecordMetricsUpdate(gp, 0);
        return wg;
    }

    private WeekEntity MakeCurrentWeek(WeekGoalEntity weekGoal)
    {
        var week = WeekEntity.Create(1, CurrentMonday);
        // Expose WeekGoalId linkage via mock
        _weekRepo.GetWeekGoalByIdAsync(WeekGoalId, Arg.Any<CancellationToken>())
                 .Returns(weekGoal);
        _weekRepo.GetByIdAsync(weekGoal.WeekId, Arg.Any<CancellationToken>())
                 .Returns(week);
        return week;
    }

    private RequestMomentBurstCommandHandler BuildHandler()
        => new(_weekRepo, _habitRepo, _aiService, _clock, _uow);

    public RequestMomentBurstCommandTests()
    {
        _clock.UtcNow.Returns(Today);
        _habitRepo.GetByWeekGoalIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                  .Returns(Array.Empty<HabitEntity>());
    }

    [Fact]
    public async Task AiDenied_NoHabitInserted_ReturnsDeniedOutcome()
    {
        var weekGoal = MakeWeekGoal(100.0);
        MakeCurrentWeek(weekGoal);

        _aiService.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                  .Returns(Result<MomentBurstResult>.Success(
                      new MomentBurstResult.Denied("Finish your core habits first.")));

        var result = await BuildHandler().Handle(
            new RequestMomentBurstCommand(WeekGoalId, "Give me something extra"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<MomentBurstOutcome.Denied>();
        ((MomentBurstOutcome.Denied)result.Value!).Message.Should().Be("Finish your core habits first.");
        await _habitRepo.DidNotReceive().AddRangeAsync(Arg.Any<IReadOnlyList<HabitEntity>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AiAccepted_InsertsHabitWithMomentBurstType()
    {
        var weekGoal = MakeWeekGoal(100.0);
        MakeCurrentWeek(weekGoal);

        _aiService.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                  .Returns(Result<MomentBurstResult>.Success(
                      new MomentBurstResult.Accepted("Quick Sprint", "Run 1km at tempo.", 1.0, "km")));

        IReadOnlyList<HabitEntity>? captured = null;
        await _habitRepo.AddRangeAsync(
            Arg.Do<IReadOnlyList<HabitEntity>>(h => captured = h),
            Arg.Any<CancellationToken>());

        var result = await BuildHandler().Handle(
            new RequestMomentBurstCommand(WeekGoalId, "Give me a run"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<MomentBurstOutcome.HabitCreated>();
        captured.Should().HaveCount(1);
        captured![0].HabitType.Should().Be(HabitType.MomentBurst);
    }

    [Fact]
    public async Task AiAccepted_WeekGoalIdLinkedCorrectly()
    {
        var weekGoal = MakeWeekGoal(100.0);
        MakeCurrentWeek(weekGoal);

        _aiService.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                  .Returns(Result<MomentBurstResult>.Success(
                      new MomentBurstResult.Accepted("Quest", "Desc", 1.0, "reps")));

        IReadOnlyList<HabitEntity>? captured = null;
        await _habitRepo.AddRangeAsync(
            Arg.Do<IReadOnlyList<HabitEntity>>(h => captured = h),
            Arg.Any<CancellationToken>());

        await BuildHandler().Handle(
            new RequestMomentBurstCommand(WeekGoalId, "More work"), default);

        captured![0].WeekGoalId.Should().Be(WeekGoalId);
    }

    [Fact]
    public async Task GpBelow100_ReturnsFailure_AiServiceNotCalled()
    {
        var weekGoal = MakeWeekGoal(gp: 85.0);
        MakeCurrentWeek(weekGoal);

        var result = await BuildHandler().Handle(
            new RequestMomentBurstCommand(WeekGoalId, "Give me more"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("100%");
        await _aiService.DidNotReceive().GenerateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CurrentWeek_WithUnspecifiedKindStartDate_IsRecognisedAsCurrentWeek()
    {
        // Simulate a week loaded from SQLite: same date but Kind.Unspecified (not Utc)
        var unspecifiedMonday = DateTime.SpecifyKind(CurrentMonday, DateTimeKind.Unspecified);
        var weekGoal = MakeWeekGoal(100.0);

        var week = WeekEntity.Create(1, unspecifiedMonday);
        _weekRepo.GetWeekGoalByIdAsync(WeekGoalId, Arg.Any<CancellationToken>())
                 .Returns(weekGoal);
        _weekRepo.GetByIdAsync(weekGoal.WeekId, Arg.Any<CancellationToken>())
                 .Returns(week);

        _aiService.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                  .Returns(Result<MomentBurstResult>.Success(
                      new MomentBurstResult.Accepted("Sprint", "Run fast.", 1.0, "km")));

        IReadOnlyList<HabitEntity>? captured = null;
        await _habitRepo.AddRangeAsync(
            Arg.Do<IReadOnlyList<HabitEntity>>(h => captured = h),
            Arg.Any<CancellationToken>());

        var result = await BuildHandler().Handle(
            new RequestMomentBurstCommand(WeekGoalId, "More"), default);

        result.IsSuccess.Should().BeTrue("Kind.Unspecified from SQLite must not be treated as a different week");
        result.Value.Should().BeOfType<MomentBurstOutcome.HabitCreated>();
        captured.Should().HaveCount(1);
    }
}
