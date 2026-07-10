using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Goal;
using LifeGrid.Domain.Week;
using LifeGrid.Domain.WeekGoal;
using NSubstitute;
using WeekEntity     = LifeGrid.Domain.Week.Week;
using WeekGoalEntity = LifeGrid.Domain.WeekGoal.WeekGoal;
using GoalEntity     = LifeGrid.Domain.Goal.Goal;

namespace LifeGrid.Application.Tests.Week;

public sealed class CloseWeekCommandTests
{
    private readonly IWeekRepository          _weekRepo    = Substitute.For<IWeekRepository>();
    private readonly IGoalRepository          _goalRepo    = Substitute.For<IGoalRepository>();
    private readonly IUnitOfWork              _uow         = Substitute.For<IUnitOfWork>();
    private readonly IEconomyStateBroadcaster _broadcaster = Substitute.For<IEconomyStateBroadcaster>();
    private readonly CloseWeekCommandHandler  _handler;

    private static readonly DateTime Monday = new(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc);

    public CloseWeekCommandTests()
    {
        _handler = new CloseWeekCommandHandler(_weekRepo, _goalRepo, _uow, _broadcaster);
    }

    // ── Baseline behaviour ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ExistingWeek_SetsStatusToClosedAndCommits()
    {
        var week = WeekEntity.Create(5, Monday);
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);

        var result = await _handler.Handle(new CloseWeekCommand(week.WeekId), default);

        result.IsSuccess.Should().BeTrue();
        week.Status.Should().Be(WeekStatus.Closed);
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        _broadcaster.Received(1).Broadcast();
    }

    [Fact]
    public async Task Handle_WeekNotFound_ReturnsFailure()
    {
        _weekRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                 .Returns((WeekEntity?)null);

        var result = await _handler.Handle(new CloseWeekCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("week_not_found");
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    // ── Escalation: Clean → Level1Warning ─────────────────────────────────────

    [Fact]
    public async Task Handle_CleanGoal_Below80Gp_SetsLevel1Warning()
    {
        var (week, weekGoal) = BuildWeekWithGoal(weekNumber: 1, gp: 79.0, xp: 100);
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _weekRepo.GetPreviousWeekGoalAsync(weekGoal.GoalId, 1, Arg.Any<CancellationToken>())
                 .Returns((WeekGoalEntity?)null);

        await _handler.Handle(new CloseWeekCommand(week.WeekId), default);

        weekGoal.PenaltyState.Should().Be(PenaltyState.Level1Warning);
        weekGoal.GoalWeeklyXpEarned.Should().Be(100);
    }

    // ── Escalation: Level1Warning → ProbationWeek2 ────────────────────────────

    [Fact]
    public async Task Handle_Level1WarningGoal_Below100Gp_HalvesXp_SetsProbation()
    {
        var (week, weekGoal) = BuildWeekWithGoal(weekNumber: 2, gp: 95.0, xp: 100);
        var prevWeekGoal     = WeekGoalEntity.Create(Guid.NewGuid(), weekGoal.GoalId, 1);
        prevWeekGoal.SetPenaltyState(PenaltyState.Level1Warning);

        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _weekRepo.GetPreviousWeekGoalAsync(weekGoal.GoalId, 2, Arg.Any<CancellationToken>())
                 .Returns(prevWeekGoal);

        await _handler.Handle(new CloseWeekCommand(week.WeekId), default);

        weekGoal.PenaltyState.Should().Be(PenaltyState.ProbationWeek2);
        weekGoal.GoalWeeklyXpEarned.Should().Be(50);
    }

    // ── Escalation: ProbationWeek2 → ReckoningWeek3 + Goal.Overwhelmed ────────

    [Fact]
    public async Task Handle_ProbationGoal_Below100Gp_ZerosXp_MarksGoalOverwhelmed()
    {
        var goalId           = Guid.NewGuid();
        var (week, weekGoal) = BuildWeekWithGoal(weekNumber: 3, gp: 99.0, xp: 80, goalId: goalId);
        var prevWeekGoal     = WeekGoalEntity.Create(Guid.NewGuid(), goalId, 2);
        prevWeekGoal.SetPenaltyState(PenaltyState.ProbationWeek2);

        var goal = GoalEntity.Create(Guid.NewGuid(), "Run", "Physical", "3 months",
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc));

        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _weekRepo.GetPreviousWeekGoalAsync(goalId, 3, Arg.Any<CancellationToken>())
                 .Returns(prevWeekGoal);
        _goalRepo.GetByIdAsync(goalId, Arg.Any<CancellationToken>()).Returns(goal);

        var result = await _handler.Handle(new CloseWeekCommand(week.WeekId), default);

        weekGoal.PenaltyState.Should().Be(PenaltyState.ReckoningWeek3);
        weekGoal.GoalWeeklyXpEarned.Should().Be(0);
        goal.Status.Should().Be(GoalStatus.Overwhelmed);
        result.Value!.OverwhelmedGoalId.Should().Be(goalId);
    }

    // ── Escalation: No previous week → defaults to Clean ──────────────────────

    [Fact]
    public async Task Handle_NoPreviousWeekGoal_TreatedAsClean_NoEscalation()
    {
        var (week, weekGoal) = BuildWeekWithGoal(weekNumber: 1, gp: 90.0, xp: 50);
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _weekRepo.GetPreviousWeekGoalAsync(weekGoal.GoalId, 1, Arg.Any<CancellationToken>())
                 .Returns((WeekGoalEntity?)null);

        await _handler.Handle(new CloseWeekCommand(week.WeekId), default);

        weekGoal.PenaltyState.Should().Be(PenaltyState.Clean);
        weekGoal.GoalWeeklyXpEarned.Should().Be(50);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static (WeekEntity week, WeekGoalEntity weekGoal) BuildWeekWithGoal(
        int weekNumber, double gp, int xp, Guid? goalId = null)
    {
        var week     = WeekEntity.Create(weekNumber, Monday);
        var weekGoal = WeekGoalEntity.Create(week.WeekId, goalId ?? Guid.NewGuid(), weekNumber);
        weekGoal.RecordMetricsUpdate(gp, xp);
        week.AddWeekGoal(weekGoal);
        return (week, weekGoal);
    }
}
