using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.Goal;
using LifeGrid.Application.ViceCheck;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.ViceCheck;
using LifeGrid.Domain.WeekGoal;
using NSubstitute;
using GoalEntity     = LifeGrid.Domain.Goal.Goal;
using WeekGoalEntity = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.Tests.ViceCheck;

public sealed class ResolveViceCheckCommandTests
{
    private readonly IViceCheckAuditRepository _auditRepo   = Substitute.For<IViceCheckAuditRepository>();
    private readonly IWeekRepository           _weekRepo    = Substitute.For<IWeekRepository>();
    private readonly IGoalRepository           _goalRepo    = Substitute.For<IGoalRepository>();
    private readonly IGeminiViceCheckService   _gemini      = Substitute.For<IGeminiViceCheckService>();
    private readonly IDateTimeProvider         _clock       = Substitute.For<IDateTimeProvider>();
    private readonly IUnitOfWork               _uow         = Substitute.For<IUnitOfWork>();
    private readonly IEconomyStateBroadcaster  _broadcaster = Substitute.For<IEconomyStateBroadcaster>();

    private static readonly DateTime FixedNow = new(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc);

    public ResolveViceCheckCommandTests() => _clock.UtcNow.Returns(FixedNow);

    private ResolveViceCheckCommandHandler BuildHandler() => new(
        _auditRepo, _weekRepo, _goalRepo, _gemini, _clock, _uow, _broadcaster);

    private static ViceCheckAudit BuildPendingAudit(Guid weekGoalId, int dangerLevel) => ViceCheckAudit.Create(
        Guid.NewGuid(), weekGoalId, Guid.NewGuid(),
        "Get fit", "Late-night snacking", dangerLevel,
        "How's your evening routine?", FixedNow.AddDays(-1));

    [Fact]
    public async Task AuditNotFound_ReturnsFailure()
    {
        _auditRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ViceCheckAudit?)null);

        var result = await BuildHandler().Handle(new ResolveViceCheckCommand(Guid.NewGuid(), "answer"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("audit_not_found");
    }

    [Fact]
    public async Task AlreadyResolved_ReturnsFailure()
    {
        var audit = BuildPendingAudit(Guid.NewGuid(), 5);
        audit.MarkPassed("prior answer", FixedNow.AddHours(-1));
        _auditRepo.GetByIdAsync(audit.AuditId, Arg.Any<CancellationToken>()).Returns(audit);

        var result = await BuildHandler().Handle(new ResolveViceCheckCommand(audit.AuditId, "answer"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("already_resolved");
    }

    [Fact]
    public async Task AiFailure_PropagatesFailure()
    {
        var audit = BuildPendingAudit(Guid.NewGuid(), 5);
        _auditRepo.GetByIdAsync(audit.AuditId, Arg.Any<CancellationToken>()).Returns(audit);
        _gemini.EvaluateAnswerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(Result<EvaluateAnswerResult>.Failure("gemini_down"));

        var result = await BuildHandler().Handle(new ResolveViceCheckCommand(audit.AuditId, "answer"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("gemini_down");
    }

    [Fact]
    public async Task PersistsFalse_NoGpChange_MarksPassed()
    {
        var weekGoalId = Guid.NewGuid();
        var audit      = BuildPendingAudit(weekGoalId, 5);
        _auditRepo.GetByIdAsync(audit.AuditId, Arg.Any<CancellationToken>()).Returns(audit);
        _gemini.EvaluateAnswerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(Result<EvaluateAnswerResult>.Success(new EvaluateAnswerResult(false)));

        var result = await BuildHandler().Handle(new ResolveViceCheckCommand(audit.AuditId, "I went to bed early"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Persists.Should().BeFalse();
        result.Value!.NewGp.Should().BeNull();
        audit.Status.Should().Be(ViceCheckStatus.Passed);
        await _weekRepo.DidNotReceive().GetWeekGoalByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PersistsTrue_DangerLevel5_Gp82_AppliesPenalty_ShiftsCleanToLevel1Warning()
    {
        var weekGoal = WeekGoalEntity.Create(Guid.NewGuid(), Guid.NewGuid(), 1);
        weekGoal.RecordMetricsUpdate(82.0, 0);
        var audit = BuildPendingAudit(weekGoal.WeekGoalId, 5);

        _auditRepo.GetByIdAsync(audit.AuditId, Arg.Any<CancellationToken>()).Returns(audit);
        _weekRepo.GetWeekGoalByIdAsync(weekGoal.WeekGoalId, Arg.Any<CancellationToken>()).Returns(weekGoal);
        _gemini.EvaluateAnswerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(Result<EvaluateAnswerResult>.Success(new EvaluateAnswerResult(true)));

        var result = await BuildHandler().Handle(
            new ResolveViceCheckCommand(audit.AuditId, "I only did it for a little bit"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Persists.Should().BeTrue();
        result.Value!.NewGp.Should().Be(77.0);
        weekGoal.GoalWeeklyGp.Should().Be(77.0);
        weekGoal.PenaltyState.Should().Be(PenaltyState.Level1Warning);
        audit.Status.Should().Be(ViceCheckStatus.Failed);
        audit.PenaltyPercentApplied.Should().Be(5.0);
    }

    [Fact]
    public async Task PersistsTrue_CascadesToOverwhelmed_WhenAlreadyProbation()
    {
        var goal = GoalEntity.Create(Guid.NewGuid(), "Get fit", "Fitness", "3 months",
            new DateTime(2026, 9, 1), new DateTime(2026, 6, 15), new DateTime(2026, 6, 8));
        var weekGoal = WeekGoalEntity.Create(Guid.NewGuid(), goal.GoalId, 1);
        weekGoal.RecordMetricsUpdate(90.0, 100);
        weekGoal.SetPenaltyState(PenaltyState.ProbationWeek2);
        var audit = BuildPendingAudit(weekGoal.WeekGoalId, 10);

        _auditRepo.GetByIdAsync(audit.AuditId, Arg.Any<CancellationToken>()).Returns(audit);
        _weekRepo.GetWeekGoalByIdAsync(weekGoal.WeekGoalId, Arg.Any<CancellationToken>()).Returns(weekGoal);
        _goalRepo.GetByIdAsync(goal.GoalId, Arg.Any<CancellationToken>()).Returns(goal);
        _gemini.EvaluateAnswerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(Result<EvaluateAnswerResult>.Success(new EvaluateAnswerResult(true)));

        var result = await BuildHandler().Handle(new ResolveViceCheckCommand(audit.AuditId, "confession"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TriggersOverwhelmed.Should().BeTrue();
        weekGoal.PenaltyState.Should().Be(PenaltyState.ReckoningWeek3);
        goal.Status.ToString().Should().Be("Overwhelmed");
    }
}
