using FluentAssertions;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Onboarding;
using LifeGrid.Application.Onboarding.Commands;
using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Onboarding;
using NSubstitute;
using System.Text.Json;
using GoalAggregate = LifeGrid.Domain.Goal.Goal;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Application.Tests.Onboarding;

public sealed class FinalizeGoalCommandHandlerTests
{
    private readonly IOnboardingRepository  _onboarding   = Substitute.For<IOnboardingRepository>();
    private readonly IUserProfileRepository _userProfiles = Substitute.For<IUserProfileRepository>();
    private readonly IGoalRepository        _goals        = Substitute.For<IGoalRepository>();
    private readonly FinalizeGoalCommandHandler _handler;

    public FinalizeGoalCommandHandlerTests()
        => _handler = new FinalizeGoalCommandHandler(_onboarding, _userProfiles, _goals);

    private static readonly ValidatedGoalDto SampleDto = new(
        "Run a marathon", "6 months",
        new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc), "Physical");

    private static readonly List<RefinementQuestionDto> SampleQuestions = new()
    {
        new(1, "What is your age and gender?"),
        new(2, "What is your current baseline?")
    };

    private static readonly DateTime SampleChosenStartDate = new(2026, 6, 22);

    private static OnboardingSession SessionInRefinementActive()
    {
        var s = OnboardingSession.Create();
        s.UpdateDraft("Run a marathon in 6 months");
        s.AdvanceToStep1();
        s.AdvanceToRefinementQuestionsActive(
            JsonSerializer.Serialize(SampleDto),
            JsonSerializer.Serialize(SampleQuestions));
        s.SetChosenStartDate(SampleChosenStartDate);
        return s;
    }

    private static readonly List<(int RankOrder, string Answer)> SampleAnswers = new()
    {
        (1, "32, male"),
        (2, "5k comfortable")
    };

    [Fact]
    public async Task NoSession_ReturnsFailure()
    {
        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns((OnboardingSession?)null);

        var result = await _handler.Handle(new FinalizeGoalCommand(SampleAnswers), default);

        result.IsSuccess.Should().BeFalse();
        await _goals.DidNotReceive().AddAsync(Arg.Any<GoalAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SessionNotInRefinementActive_ReturnsFailure()
    {
        var session = OnboardingSession.Create();
        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);

        var result = await _handler.Handle(new FinalizeGoalCommand(SampleAnswers), default);

        result.IsSuccess.Should().BeFalse();
        await _goals.DidNotReceive().AddAsync(Arg.Any<GoalAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoUserProfile_ReturnsFailure()
    {
        var session = SessionInRefinementActive();
        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns((UserProfileEntity?)null);

        var result = await _handler.Handle(new FinalizeGoalCommand(SampleAnswers), default);

        result.IsSuccess.Should().BeFalse();
        await _goals.DidNotReceive().AddAsync(Arg.Any<GoalAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidFlow_CreatesGoalAndRefinementAnswers()
    {
        var session = SessionInRefinementActive();
        var profile = UserProfileEntity.Create();
        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _onboarding.UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>())
                   .Returns(x => x.Arg<OnboardingSession>());
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);

        GoalAggregate? savedGoal = null;
        await _goals.AddAsync(Arg.Do<GoalAggregate>(g => savedGoal = g), Arg.Any<CancellationToken>());

        var result = await _handler.Handle(new FinalizeGoalCommand(SampleAnswers), default);

        result.IsSuccess.Should().BeTrue();
        savedGoal.Should().NotBeNull();
        savedGoal!.UserId.Should().Be(profile.UserId);
        savedGoal.Description.Should().Be("Run a marathon");
        savedGoal.RefinementAnswers.Should().HaveCount(2);
        savedGoal.RefinementAnswers.First(r => r.RankOrder == 1).Answer.Should().Be("32, male");
        savedGoal.StartDate.Should().Be(SampleChosenStartDate);
        savedGoal.StartDate.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public async Task ValidFlow_AdvancesSessionToExecutionVerified()
    {
        var session = SessionInRefinementActive();
        var profile = UserProfileEntity.Create();
        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _onboarding.UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>())
                   .Returns(x => x.Arg<OnboardingSession>());
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);

        await _handler.Handle(new FinalizeGoalCommand(SampleAnswers), default);

        session.CurrentStep.Should().Be(OnboardingStep.Step1_ExecutionVerified);
    }

    [Fact]
    public async Task ValidFlow_ClearsStagingJsonAfterCommit()
    {
        var session = SessionInRefinementActive();
        var profile = UserProfileEntity.Create();
        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _onboarding.UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>())
                   .Returns(x => x.Arg<OnboardingSession>());
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);

        await _handler.Handle(new FinalizeGoalCommand(SampleAnswers), default);

        session.ValidatedGoalJson.Should().BeNull();
        session.RefinementQuestionsJson.Should().BeNull();
    }

    [Fact]
    public async Task GoalNotWrittenBeforeExplicitCall()
    {
        // No command is dispatched — goal repository must never be called
        await _goals.DidNotReceive().AddAsync(Arg.Any<GoalAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidFlow_UsesChosenStartDateFromSession()
    {
        var chosenStart = new DateTime(2026, 7, 6); // Monday
        var session     = OnboardingSession.Create();
        session.UpdateDraft("Run a marathon in 6 months");
        session.AdvanceToStep1();
        session.AdvanceToRefinementQuestionsActive(
            JsonSerializer.Serialize(SampleDto),
            JsonSerializer.Serialize(SampleQuestions));
        session.SetChosenStartDate(chosenStart);

        var profile = UserProfileEntity.Create();
        _onboarding.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _onboarding.UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>())
                   .Returns(x => x.Arg<OnboardingSession>());
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);

        GoalAggregate? savedGoal = null;
        await _goals.AddAsync(Arg.Do<GoalAggregate>(g => savedGoal = g), Arg.Any<CancellationToken>());

        await _handler.Handle(new FinalizeGoalCommand(SampleAnswers), default);

        savedGoal.Should().NotBeNull();
        savedGoal!.StartDate.Should().Be(chosenStart);
    }
}
