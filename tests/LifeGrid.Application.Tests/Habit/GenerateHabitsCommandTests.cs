using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Habit;
using LifeGrid.Application.Onboarding;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Week;
using LifeGrid.Application.Week.Commands;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.Onboarding;
using NSubstitute;
using GoalAggregate     = LifeGrid.Domain.Goal.Goal;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;
using WeekEntity        = LifeGrid.Domain.Week.Week;
using WeekGoalEntity    = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.Tests.Habit;

public sealed class GenerateScheduleCommandTests
{
    private readonly IOnboardingRepository         _onboarding   = Substitute.For<IOnboardingRepository>();
    private readonly IUserProfileRepository        _userProfiles = Substitute.For<IUserProfileRepository>();
    private readonly IGoalRepository               _goals        = Substitute.For<IGoalRepository>();
    private readonly IGeminiHabitGenerationService _aiService    = Substitute.For<IGeminiHabitGenerationService>();
    private readonly IWeekRepository               _weekRepo     = Substitute.For<IWeekRepository>();
    private readonly IHabitRepository              _habitRepo    = Substitute.For<IHabitRepository>();
    private readonly IUnitOfWork                   _uow          = Substitute.For<IUnitOfWork>();

    private readonly GenerateScheduleCommandHandler _handler;

    public GenerateScheduleCommandTests()
        => _handler = new GenerateScheduleCommandHandler(
            _onboarding, _userProfiles, _goals, _aiService, _weekRepo, _habitRepo, _uow);

    // ── helpers ──────────────────────────────────────────────────────────────

    private static readonly DateTime SampleChosenStartDate = new(2026, 6, 22);

    private const string SampleBlueprintJson =
        """{"isFeasible":true,"coaching_strategy_summary":"Run 4x/week","schedule_parameters":{"measurement_unit":"km","starting_week_load":10,"peak_form_week_number":20,"peak_week_measurement_parameter":35,"peak_week_milestone_description":"Long run"}}""";

    private static OnboardingSession SessionAtExecutionVerifiedWithBlueprint(Guid goalId)
    {
        var s = OnboardingSession.Create();
        s.UpdateDraft("Run a marathon in 6 months");
        s.AdvanceToStep1();
        s.AdvanceToRefinementQuestionsActive("{}", "[]");
        s.AdvanceToExecutionVerified();
        s.SetChosenStartDate(SampleChosenStartDate);
        s.SetGoal(goalId);
        s.CacheBlueprint(SampleBlueprintJson);
        return s;
    }

    private static GoalAggregate SampleGoal(Guid userId)
    {
        var goal = GoalAggregate.Create(
            userId, "Run a marathon", "Physical", "6 months",
            new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22),
            new DateTime(2026, 6, 16));
        goal.SetRefinementAnswers(new[] { (1, "What is your baseline?", (string?)"5k comfortable") });
        return goal;
    }

    private static readonly IReadOnlyList<WeekScheduleDto> SampleSchedule =
        new List<WeekScheduleDto>
        {
            new(1, new DateTime(2026, 6, 16), new List<HabitScheduleItemDto>
            {
                new("Run 3 km", 3.0, "km")
            })
        };

    private static readonly HabitSchedulingResult FeasibleResult =
        new HabitSchedulingResult.Feasible(SampleSchedule);

    // ── guard tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task NoActiveSession_ReturnsFailure()
    {
        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns((OnboardingSession?)null);

        var result = await _handler.Handle(new GenerateScheduleCommand(), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SessionNotInExecutionVerified_ReturnsFailure()
    {
        var session = OnboardingSession.Create(); // Unstarted
        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);

        var result = await _handler.Handle(new GenerateScheduleCommand(), default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task NoGoalId_ReturnsFailure()
    {
        var session = OnboardingSession.Create();
        session.UpdateDraft("Run a marathon in 6 months");
        session.AdvanceToStep1();
        session.AdvanceToRefinementQuestionsActive("{}", "[]");
        session.AdvanceToExecutionVerified();
        session.SetChosenStartDate(SampleChosenStartDate);
        // no SetGoal call

        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);

        var result = await _handler.Handle(new GenerateScheduleCommand(), default);

        result.IsSuccess.Should().BeFalse();
        await _goals.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoUserProfile_ReturnsFailure()
    {
        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>())
                   .Returns(SessionAtExecutionVerifiedWithBlueprint(Guid.NewGuid()));
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns((UserProfileEntity?)null);

        var result = await _handler.Handle(new GenerateScheduleCommand(), default);

        result.IsSuccess.Should().BeFalse();
        await _goals.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GoalNotFound_ReturnsFailure()
    {
        var goalId  = Guid.NewGuid();
        var profile = UserProfileEntity.Create();
        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>())
                   .Returns(SessionAtExecutionVerifiedWithBlueprint(goalId));
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goals.GetByIdAsync(goalId, Arg.Any<CancellationToken>())
              .Returns((GoalAggregate?)null);

        var result = await _handler.Handle(new GenerateScheduleCommand(), default);

        result.IsSuccess.Should().BeFalse();
        await _aiService.DidNotReceive().GenerateScheduleFromBlueprintAsync(
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoBlueprintCached_ReturnsFailure()
    {
        var profile = UserProfileEntity.Create();
        var goal    = SampleGoal(profile.UserId);

        var session = OnboardingSession.Create();
        session.UpdateDraft("Run a marathon in 6 months");
        session.AdvanceToStep1();
        session.AdvanceToRefinementQuestionsActive("{}", "[]");
        session.AdvanceToExecutionVerified();
        session.SetChosenStartDate(SampleChosenStartDate);
        session.SetGoal(goal.GoalId);
        // no CacheBlueprint call

        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goals.GetByIdAsync(goal.GoalId, Arg.Any<CancellationToken>()).Returns(goal);

        var result = await _handler.Handle(new GenerateScheduleCommand(), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("blueprint");
    }

    // ── technical failure path ────────────────────────────────────────────────

    [Fact]
    public async Task AiServiceReturnsFailure_ReturnsFailureResult()
    {
        var profile = UserProfileEntity.Create();
        var goal    = SampleGoal(profile.UserId);

        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>())
                   .Returns(SessionAtExecutionVerifiedWithBlueprint(goal.GoalId));
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goals.GetByIdAsync(goal.GoalId, Arg.Any<CancellationToken>()).Returns(goal);
        _aiService.GenerateScheduleFromBlueprintAsync(
                Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<HabitSchedulingResult>.Failure("Gemini rate limit reached."));

        var result = await _handler.Handle(new GenerateScheduleCommand(), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("rate limit");
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task HappyPath_ReturnsComplete()
    {
        var profile = UserProfileEntity.Create();
        var goal    = SampleGoal(profile.UserId);
        ArrangeHappyPath(SessionAtExecutionVerifiedWithBlueprint(goal.GoalId), goal, profile);

        var result = await _handler.Handle(new GenerateScheduleCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<HabitGenerationOutcome.Complete>();
    }

    [Fact]
    public async Task HappyPath_WeekRepoCalledOncePerWeek()
    {
        var profile = UserProfileEntity.Create();
        var goal    = SampleGoal(profile.UserId);
        ArrangeHappyPath(SessionAtExecutionVerifiedWithBlueprint(goal.GoalId), goal, profile);

        await _handler.Handle(new GenerateScheduleCommand(), default);

        await _weekRepo.Received(1).AddAsync(
            Arg.Any<WeekEntity>(), Arg.Any<WeekGoalEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HappyPath_HabitRepoCalledOncePerWeek()
    {
        var profile = UserProfileEntity.Create();
        var goal    = SampleGoal(profile.UserId);
        ArrangeHappyPath(SessionAtExecutionVerifiedWithBlueprint(goal.GoalId), goal, profile);

        await _handler.Handle(new GenerateScheduleCommand(), default);

        await _habitRepo.Received(1).AddRangeAsync(
            Arg.Any<IReadOnlyList<LifeGrid.Domain.Habit.Habit>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HappyPath_UnitOfWorkCommittedOnce()
    {
        var profile = UserProfileEntity.Create();
        var goal    = SampleGoal(profile.UserId);
        ArrangeHappyPath(SessionAtExecutionVerifiedWithBlueprint(goal.GoalId), goal, profile);

        await _handler.Handle(new GenerateScheduleCommand(), default);

        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HappyPath_DeletesSessionAfterCompletion()
    {
        var profile = UserProfileEntity.Create();
        var goal    = SampleGoal(profile.UserId);
        ArrangeHappyPath(SessionAtExecutionVerifiedWithBlueprint(goal.GoalId), goal, profile);

        await _handler.Handle(new GenerateScheduleCommand(), default);

        await _onboarding.Received(1).DeleteAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>());
        await _onboarding.DidNotReceive().UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>());
    }

    // ── shield bonus ─────────────────────────────────────────────────────────

    [Fact]
    public async Task FirstGoal_GrantsBonusShield()
    {
        var profile = UserProfileEntity.Create();
        var goal    = SampleGoal(profile.UserId);
        var session = SessionAtExecutionVerifiedWithBlueprint(goal.GoalId);

        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goals.GetByIdAsync(goal.GoalId, Arg.Any<CancellationToken>()).Returns(goal);
        _goals.GetActiveCountAsync(profile.UserId, Arg.Any<CancellationToken>()).Returns(1);
        _aiService.GenerateScheduleFromBlueprintAsync(
                Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<HabitSchedulingResult>.Success(FeasibleResult));

        await _handler.Handle(new GenerateScheduleCommand(), default);

        profile.Economy.ShieldsAvailable.Should().Be(1);
    }

    [Fact]
    public async Task SubsequentGoal_DoesNotGrantShield()
    {
        var profile = UserProfileEntity.Create();
        var goal    = SampleGoal(profile.UserId);
        var session = SessionAtExecutionVerifiedWithBlueprint(goal.GoalId);

        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goals.GetByIdAsync(goal.GoalId, Arg.Any<CancellationToken>()).Returns(goal);
        _goals.GetActiveCountAsync(profile.UserId, Arg.Any<CancellationToken>()).Returns(2);
        _aiService.GenerateScheduleFromBlueprintAsync(
                Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<HabitSchedulingResult>.Success(FeasibleResult));

        await _handler.Handle(new GenerateScheduleCommand(), default);

        profile.Economy.ShieldsAvailable.Should().Be(0);
    }

    // ── deduplication ────────────────────────────────────────────────────────

    [Fact]
    public async Task ExistingWeekForStartDate_ReusesWeekId_CallsAddWeekGoalAsync()
    {
        var profile = UserProfileEntity.Create();
        var goal    = SampleGoal(profile.UserId);
        var session = SessionAtExecutionVerifiedWithBlueprint(goal.GoalId);

        var existingWeek = WeekEntity.Create(1, SampleSchedule[0].StartDate);
        _weekRepo.GetByStartDateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns(existingWeek);

        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goals.GetByIdAsync(goal.GoalId, Arg.Any<CancellationToken>()).Returns(goal);
        _aiService.GenerateScheduleFromBlueprintAsync(
                Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<HabitSchedulingResult>.Success(FeasibleResult));

        await _handler.Handle(new GenerateScheduleCommand(), default);

        await _weekRepo.DidNotReceive().AddAsync(
            Arg.Any<WeekEntity>(), Arg.Any<WeekGoalEntity>(), Arg.Any<CancellationToken>());
        await _weekRepo.Received(1).AddWeekGoalAsync(
            Arg.Any<WeekGoalEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WeekGoalNumber_IsOneForSingleWeekSchedule()
    {
        var profile = UserProfileEntity.Create();
        var goal    = SampleGoal(profile.UserId);
        var session = SessionAtExecutionVerifiedWithBlueprint(goal.GoalId);

        WeekGoalEntity? capturedWeekGoal = null;

        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goals.GetByIdAsync(goal.GoalId, Arg.Any<CancellationToken>()).Returns(goal);
        _aiService.GenerateScheduleFromBlueprintAsync(
                Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<HabitSchedulingResult>.Success(FeasibleResult));
        await _weekRepo.AddAsync(
            Arg.Any<WeekEntity>(),
            Arg.Do<WeekGoalEntity>(wg => capturedWeekGoal = wg),
            Arg.Any<CancellationToken>());

        await _handler.Handle(new GenerateScheduleCommand(), default);

        capturedWeekGoal.Should().NotBeNull();
        capturedWeekGoal!.WeekGoalNumber.Should().Be(1);
    }

    // ── start date forwarding ─────────────────────────────────────────────────

    [Fact]
    public async Task GenerateSchedule_PassesChosenStartDateFromSession()
    {
        var chosenStart = new DateTime(2026, 7, 6); // Monday
        var profile     = UserProfileEntity.Create();
        var goal        = SampleGoal(profile.UserId);

        var session = OnboardingSession.Create();
        session.UpdateDraft("Run a marathon in 6 months");
        session.AdvanceToStep1();
        session.AdvanceToRefinementQuestionsActive("{}", "[]");
        session.AdvanceToExecutionVerified();
        session.SetChosenStartDate(chosenStart);
        session.SetGoal(goal.GoalId);
        session.CacheBlueprint(SampleBlueprintJson);

        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goals.GetByIdAsync(goal.GoalId, Arg.Any<CancellationToken>()).Returns(goal);
        _aiService.GenerateScheduleFromBlueprintAsync(
                Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<HabitSchedulingResult>.Success(FeasibleResult));

        await _handler.Handle(new GenerateScheduleCommand(), default);

        await _aiService.Received(1).GenerateScheduleFromBlueprintAsync(
            Arg.Any<string>(), chosenStart, Arg.Any<CancellationToken>());
    }

    // ── wiring helper ─────────────────────────────────────────────────────────

    private void ArrangeHappyPath(OnboardingSession session, GoalAggregate goal, UserProfileEntity profile)
    {
        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goals.GetByIdAsync(goal.GoalId, Arg.Any<CancellationToken>()).Returns(goal);
        _aiService.GenerateScheduleFromBlueprintAsync(
                Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<HabitSchedulingResult>.Success(FeasibleResult));
    }
}
