using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.Goal;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.ViceCheck;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using NSubstitute;
using GoalEntity          = LifeGrid.Domain.Goal.Goal;
using UserProfileEntity   = LifeGrid.Domain.UserProfile.UserProfile;
using WeekEntity          = LifeGrid.Domain.Week.Week;
using WeekGoalEntity      = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.Tests.ViceCheck;

public sealed class InitiateViceCheckCommandTests
{
    private readonly IWeekRepository           _weekRepo    = Substitute.For<IWeekRepository>();
    private readonly IGoalRepository           _goalRepo    = Substitute.For<IGoalRepository>();
    private readonly IUserProfileRepository    _profileRepo = Substitute.For<IUserProfileRepository>();
    private readonly IViceCheckAuditRepository _auditRepo   = Substitute.For<IViceCheckAuditRepository>();
    private readonly IGeminiViceCheckService   _gemini      = Substitute.For<IGeminiViceCheckService>();
    private readonly IRandomProvider           _random      = Substitute.For<IRandomProvider>();
    private readonly IDateTimeProvider         _clock       = Substitute.For<IDateTimeProvider>();
    private readonly IUnitOfWork               _uow         = Substitute.For<IUnitOfWork>();
    private readonly IEconomyStateBroadcaster  _broadcaster = Substitute.For<IEconomyStateBroadcaster>();

    private static readonly DateTime WeekStartDate = new(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc); // Monday
    private static readonly DateTime FixedNow      = WeekStartDate.AddDays(6).AddHours(10);        // within window

    public InitiateViceCheckCommandTests()
    {
        _clock.UtcNow.Returns(FixedNow);
        _auditRepo.HasAuditForWeekAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        _auditRepo.GetPreviousQuestionsForBadHabitAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                  .Returns(Array.Empty<string>());
        _random.Next(Arg.Any<int>()).Returns(0);
        _gemini.GenerateQuestionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(Result<GenerateQuestionResult>.Success(new GenerateQuestionResult("How's your evening routine?")));
    }

    private InitiateViceCheckCommandHandler BuildHandler() => new(
        _weekRepo, _goalRepo, _profileRepo, _auditRepo, _gemini, _random, _clock, _uow, _broadcaster);

    private static (WeekEntity Week, GoalEntity Goal) BuildClosedWeekWithBadHabit(
        string badHabitDescription = "Late-night snacking", int dangerLevel = 5)
    {
        var week = WeekEntity.Create(1, WeekStartDate);
        week.Close();
        var goal = GoalEntity.Create(Guid.NewGuid(), "Get fit", "Fitness", "3 months",
            new DateTime(2026, 9, 1), WeekStartDate, WeekStartDate.AddDays(-7));
        goal.SetLinkedBadHabits(new[] { (badHabitDescription, dangerLevel) });
        var weekGoal = WeekGoalEntity.Create(week.WeekId, goal.GoalId, 1);
        week.AddWeekGoal(weekGoal);
        return (week, goal);
    }

    private static UserProfileEntity BuildSurveyCompletedProfile()
    {
        var profile = UserProfileEntity.Create();
        profile.GrantSurveyBonusShield(); // sets IsViceSurveyCompleted = true
        return profile;
    }

    [Fact]
    public async Task WeekNotFound_ReturnsFailure()
    {
        _weekRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((WeekEntity?)null);

        var result = await BuildHandler().Handle(new InitiateViceCheckCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task WeekNotClosed_ReturnsFailure()
    {
        var week = WeekEntity.Create(1, WeekStartDate); // Active, not closed
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);

        var result = await BuildHandler().Handle(new InitiateViceCheckCommand(week.WeekId), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("week_not_closed");
    }

    [Fact]
    public async Task SurveyNotCompleted_ReturnsFailure()
    {
        var (week, _) = BuildClosedWeekWithBadHabit();
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(UserProfileEntity.Create());

        var result = await BuildHandler().Handle(new InitiateViceCheckCommand(week.WeekId), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("vice_survey_not_completed");
    }

    [Fact]
    public async Task AlreadyAudited_ReturnsFailure()
    {
        var (week, _) = BuildClosedWeekWithBadHabit();
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(BuildSurveyCompletedProfile());
        _auditRepo.HasAuditForWeekAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await BuildHandler().Handle(new InitiateViceCheckCommand(week.WeekId), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("already_audited");
    }

    [Fact]
    public async Task WindowExpired_ReturnsFailure()
    {
        var (week, _) = BuildClosedWeekWithBadHabit();
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(BuildSurveyCompletedProfile());
        _clock.UtcNow.Returns(WeekStartDate.AddDays(6).AddHours(73)); // past 72h cutoff

        var result = await BuildHandler().Handle(new InitiateViceCheckCommand(week.WeekId), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("window_expired");
    }

    [Fact]
    public async Task NoBadHabitsAvailable_ReturnsFailure()
    {
        var week = WeekEntity.Create(1, WeekStartDate);
        week.Close();
        var goal = GoalEntity.Create(Guid.NewGuid(), "Get fit", "Fitness", "3 months",
            new DateTime(2026, 9, 1), WeekStartDate, WeekStartDate.AddDays(-7)); // no bad habits
        var weekGoal = WeekGoalEntity.Create(week.WeekId, goal.GoalId, 1);
        week.AddWeekGoal(weekGoal);

        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(BuildSurveyCompletedProfile());
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(new List<GoalEntity> { goal });

        var result = await BuildHandler().Handle(new InitiateViceCheckCommand(week.WeekId), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("no_bad_habits_available");
    }

    [Fact]
    public async Task HappyPath_AwardsXpAndCreatesAudit()
    {
        var (week, goal) = BuildClosedWeekWithBadHabit();
        var profile = BuildSurveyCompletedProfile();
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(new List<GoalEntity> { goal });

        var result = await BuildHandler().Handle(new InitiateViceCheckCommand(week.WeekId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Question.Should().Be("How's your evening routine?");
        await _auditRepo.Received(1).AddAsync(Arg.Any<Domain.ViceCheck.ViceCheckAudit>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HappyPath_AwardsXp_IndependentOfLaterOutcome()
    {
        var (week, goal) = BuildClosedWeekWithBadHabit();
        var profile = BuildSurveyCompletedProfile();
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(new List<GoalEntity> { goal });

        var xpBefore = profile.Economy.LifetimeXp;
        var result   = await BuildHandler().Handle(new InitiateViceCheckCommand(week.WeekId), default);

        result.IsSuccess.Should().BeTrue();
        profile.Economy.LifetimeXp.Should().Be(xpBefore + 20);
    }

    [Fact]
    public async Task RandomProvider_UsedToSelectAmongMultipleTriples()
    {
        var week = WeekEntity.Create(1, WeekStartDate);
        week.Close();
        var goal = GoalEntity.Create(Guid.NewGuid(), "Get fit", "Fitness", "3 months",
            new DateTime(2026, 9, 1), WeekStartDate, WeekStartDate.AddDays(-7));
        goal.SetLinkedBadHabits(new[] { ("Vice A", 2), ("Vice B", 7) });
        var weekGoal = WeekGoalEntity.Create(week.WeekId, goal.GoalId, 1);
        week.AddWeekGoal(weekGoal);

        var secondHabit = goal.LinkedBadHabits.ElementAt(1);

        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(BuildSurveyCompletedProfile());
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(new List<GoalEntity> { goal });
        _random.Next(Arg.Any<int>()).Returns(1); // force selection of the second triple

        var result = await BuildHandler().Handle(new InitiateViceCheckCommand(week.WeekId), default);

        result.IsSuccess.Should().BeTrue();
        await _auditRepo.Received(1).AddAsync(
            Arg.Is<Domain.ViceCheck.ViceCheckAudit>(a =>
                a.BadHabitId == secondHabit.BadHabitId && a.WeekGoalId == weekGoal.WeekGoalId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IncludesPreviousQuestionsForSelectedBadHabit_InGeminiPayload()
    {
        var (week, goal) = BuildClosedWeekWithBadHabit();
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(BuildSurveyCompletedProfile());
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(new List<GoalEntity> { goal });
        _auditRepo.GetPreviousQuestionsForBadHabitAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                  .Returns(new List<string> { "Did you snack at midnight?", "How was your fridge discipline?" });

        string? capturedPayload = null;
        _gemini.GenerateQuestionAsync(Arg.Do<string>(p => capturedPayload = p), Arg.Any<CancellationToken>())
               .Returns(Result<GenerateQuestionResult>.Success(new GenerateQuestionResult("How's your evening routine?")));

        await BuildHandler().Handle(new InitiateViceCheckCommand(week.WeekId), default);

        capturedPayload.Should().NotBeNull();
        capturedPayload.Should().Contain("Did you snack at midnight?");
        capturedPayload.Should().Contain("How was your fridge discipline?");
    }

    [Fact]
    public async Task NoPriorAudits_SendsEmptyPreviousQuestionsArray()
    {
        var (week, goal) = BuildClosedWeekWithBadHabit();
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(BuildSurveyCompletedProfile());
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(new List<GoalEntity> { goal });

        string? capturedPayload = null;
        _gemini.GenerateQuestionAsync(Arg.Do<string>(p => capturedPayload = p), Arg.Any<CancellationToken>())
               .Returns(Result<GenerateQuestionResult>.Success(new GenerateQuestionResult("How's your evening routine?")));

        await BuildHandler().Handle(new InitiateViceCheckCommand(week.WeekId), default);

        capturedPayload.Should().NotBeNull();
        capturedPayload.Should().Contain("\"previous_questions\":[]");
    }
}
