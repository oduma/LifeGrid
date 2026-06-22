using FluentAssertions;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Onboarding;
using LifeGrid.Application.Week;
using LifeGrid.Application.Week.Commands;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.Onboarding;
using NSubstitute;
using GoalAggregate = LifeGrid.Domain.Goal.Goal;

namespace LifeGrid.Application.Tests.Habit;

public sealed class GenerateBlueprintCommandTests
{
    private readonly IOnboardingRepository         _onboarding = Substitute.For<IOnboardingRepository>();
    private readonly IGoalRepository               _goals      = Substitute.For<IGoalRepository>();
    private readonly IGeminiHabitGenerationService _aiService  = Substitute.For<IGeminiHabitGenerationService>();

    private readonly GenerateBlueprintCommandHandler _handler;

    public GenerateBlueprintCommandTests()
        => _handler = new GenerateBlueprintCommandHandler(_onboarding, _goals, _aiService);

    // ── helpers ──────────────────────────────────────────────────────────────

    private static readonly DateTime SampleChosenStartDate = new(2026, 6, 22);

    private static OnboardingSession SessionAtExecutionVerified(Guid? goalId = null)
    {
        var s = OnboardingSession.Create();
        s.UpdateDraft("Run a marathon in 6 months");
        s.AdvanceToStep1();
        s.AdvanceToRefinementQuestionsActive("{}", "[]");
        s.AdvanceToExecutionVerified();
        s.SetChosenStartDate(SampleChosenStartDate);
        if (goalId.HasValue)
            s.SetGoal(goalId.Value);
        return s;
    }

    private static GoalAggregate SampleGoal()
    {
        var goal = GoalAggregate.Create(
            Guid.NewGuid(), "Run a marathon", "Physical", "6 months",
            new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22),
            new DateTime(2026, 6, 16));
        goal.SetRefinementAnswers(new[] { (1, "What is your baseline?", (string?)"5k comfortable") });
        return goal;
    }

    private const string SampleBlueprintJson =
        """{"isFeasible":true,"coaching_strategy_summary":"Run 4x/week","schedule_parameters":{"measurement_unit":"km","starting_week_load":10,"peak_form_week_number":20,"peak_week_measurement_parameter":35,"peak_week_milestone_description":"Long run"}}""";

    // ── guard tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task NoActiveSession_ReturnsFailure()
    {
        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns((OnboardingSession?)null);

        var result = await _handler.Handle(new GenerateBlueprintCommand(), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SessionNotInExecutionVerified_ReturnsFailure()
    {
        var session = OnboardingSession.Create(); // Unstarted
        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);

        var result = await _handler.Handle(new GenerateBlueprintCommand(), default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task NoGoalId_ReturnsFailure()
    {
        // Session at ExecutionVerified but no SetGoal call
        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>())
                   .Returns(SessionAtExecutionVerified(goalId: null));

        var result = await _handler.Handle(new GenerateBlueprintCommand(), default);

        result.IsSuccess.Should().BeFalse();
        await _goals.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GoalNotFound_ReturnsFailure()
    {
        var goalId  = Guid.NewGuid();
        var session = SessionAtExecutionVerified(goalId);
        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _goals.GetByIdAsync(goalId, Arg.Any<CancellationToken>()).Returns((GoalAggregate?)null);

        var result = await _handler.Handle(new GenerateBlueprintCommand(), default);

        result.IsSuccess.Should().BeFalse();
        await _aiService.DidNotReceive().GenerateBlueprintAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    // ── cache hit ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CacheHit_BlueprintAlreadySet_ReturnsBlueprintReadyWithoutCallingAI()
    {
        var goal    = SampleGoal();
        var session = SessionAtExecutionVerified(goal.GoalId);
        session.CacheBlueprint(SampleBlueprintJson);

        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);

        var result = await _handler.Handle(new GenerateBlueprintCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<HabitGenerationOutcome.Complete>();
        await _aiService.DidNotReceive().GenerateBlueprintAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _goals.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── cache miss: happy path ────────────────────────────────────────────────

    [Fact]
    public async Task CacheMiss_CallsAIAndSavesBlueprintToSession()
    {
        var goal    = SampleGoal();
        var session = SessionAtExecutionVerified(goal.GoalId);

        OnboardingSession? upsertedSession = null;
        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _onboarding.UpsertAsync(Arg.Do<OnboardingSession>(s => upsertedSession = s), Arg.Any<CancellationToken>())
                   .Returns(x => x.Arg<OnboardingSession>());
        _goals.GetByIdAsync(goal.GoalId, Arg.Any<CancellationToken>()).Returns(goal);
        _aiService.GenerateBlueprintAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<BlueprintResult>.Success(new BlueprintResult.Feasible(SampleBlueprintJson)));

        var result = await _handler.Handle(new GenerateBlueprintCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<HabitGenerationOutcome.Complete>();
        upsertedSession.Should().NotBeNull();
        upsertedSession!.BlueprintJson.Should().Be(SampleBlueprintJson);
    }

    // ── infeasibility path ────────────────────────────────────────────────────

    [Fact]
    public async Task AIReturnsInfeasible_ReturnsInfeasibleOutcomeAndDoesNotCache()
    {
        var goal    = SampleGoal();
        var session = SessionAtExecutionVerified(goal.GoalId);

        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _goals.GetByIdAsync(goal.GoalId, Arg.Any<CancellationToken>()).Returns(goal);
        _aiService.GenerateBlueprintAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<BlueprintResult>.Success(
                new BlueprintResult.Infeasible("Too aggressive", "2027-06-01", null)));

        var result = await _handler.Handle(new GenerateBlueprintCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<HabitGenerationOutcome.Infeasible>();
        ((HabitGenerationOutcome.Infeasible)result.Value!).RecalibrationReason.Should().Be("Too aggressive");
        await _onboarding.DidNotReceive().UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>());
    }

    // ── technical failure ─────────────────────────────────────────────────────

    [Fact]
    public async Task AIFailure_ReturnsFailureResult()
    {
        var goal    = SampleGoal();
        var session = SessionAtExecutionVerified(goal.GoalId);

        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _goals.GetByIdAsync(goal.GoalId, Arg.Any<CancellationToken>()).Returns(goal);
        _aiService.GenerateBlueprintAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<BlueprintResult>.Failure("Gemini rate limit reached."));

        var result = await _handler.Handle(new GenerateBlueprintCommand(), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("rate limit");
    }
}
