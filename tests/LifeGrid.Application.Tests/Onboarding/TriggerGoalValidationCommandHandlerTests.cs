using FluentAssertions;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Onboarding;
using LifeGrid.Application.Onboarding.Commands;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.Onboarding;
using NSubstitute;

namespace LifeGrid.Application.Tests.Onboarding;

public sealed class TriggerGoalValidationCommandHandlerTests
{
    private readonly IOnboardingRepository        _repository = Substitute.For<IOnboardingRepository>();
    private readonly IGeminiGoalValidationService _gemini     = Substitute.For<IGeminiGoalValidationService>();
    private readonly TriggerGoalValidationCommandHandler _handler;

    public TriggerGoalValidationCommandHandlerTests()
        => _handler = new TriggerGoalValidationCommandHandler(_repository, _gemini);

    private static OnboardingSession SessionWithDraft(string draft)
    {
        var s = OnboardingSession.Create();
        s.UpdateDraft(draft);
        s.AdvanceToStep1();
        return s;
    }

    [Fact]
    public async Task NoSession_ReturnsFailure()
    {
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns((OnboardingSession?)null);

        var result = await _handler.Handle(new TriggerGoalValidationCommand(), default);

        result.IsSuccess.Should().BeFalse();
        await _gemini.DidNotReceive().ValidateGoalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmptyDraft_ReturnsFailure()
    {
        var session = OnboardingSession.Create();
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);

        var result = await _handler.Handle(new TriggerGoalValidationCommand(), default);

        result.IsSuccess.Should().BeFalse();
        await _gemini.DidNotReceive().ValidateGoalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GeminiReturnsInvalid_RevertsSessionToGoalDraftCapturedAndReturnsFailure()
    {
        var session = SessionWithDraft("vague goal");
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _repository.UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>())
                   .Returns(x => x.Arg<OnboardingSession>());
        _gemini.ValidateGoalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(Result<GeminiValidationResult>.Success(
                   new GeminiValidationResult.Invalid("Please add a deadline.")));

        var result = await _handler.Handle(new TriggerGoalValidationCommand(), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Please add a deadline.");
        session.CurrentStep.Should().Be(OnboardingStep.Step1_GoalDraftCaptured);
        await _gemini.DidNotReceive().GenerateRefinementQuestionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GeminiServiceError_RevertsSessionAndReturnsFailure()
    {
        var session = SessionWithDraft("Run a marathon in 6 months");
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _repository.UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>())
                   .Returns(x => x.Arg<OnboardingSession>());
        _gemini.ValidateGoalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(Result<GeminiValidationResult>.Failure("API error."));

        var result = await _handler.Handle(new TriggerGoalValidationCommand(), default);

        result.IsSuccess.Should().BeFalse();
        session.CurrentStep.Should().Be(OnboardingStep.Step1_GoalDraftCaptured);
    }

    [Fact]
    public async Task GeminiRefinementFails_RevertsSessionAndReturnsFailure()
    {
        var session = SessionWithDraft("Run a marathon in 6 months");
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _repository.UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>())
                   .Returns(x => x.Arg<OnboardingSession>());

        var validDto = new ValidatedGoalDto("Run a marathon", "6 months",
            new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc), "Physical");
        _gemini.ValidateGoalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(Result<GeminiValidationResult>.Success(new GeminiValidationResult.Valid(validDto)));
        _gemini.GenerateRefinementQuestionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(Result<IReadOnlyList<RefinementQuestionDto>>.Failure("Prompt2 failed."));

        var result = await _handler.Handle(new TriggerGoalValidationCommand(), default);

        result.IsSuccess.Should().BeFalse();
        session.CurrentStep.Should().Be(OnboardingStep.Step1_GoalDraftCaptured);
    }

    [Fact]
    public async Task ValidGoal_AdvancesSessionToRefinementActiveAndReturnsQuestions()
    {
        var session = SessionWithDraft("Run a marathon in 6 months");
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _repository.UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>())
                   .Returns(x => x.Arg<OnboardingSession>());

        var validDto = new ValidatedGoalDto("Run a marathon", "6 months",
            new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc), "Physical");
        var questions = new List<RefinementQuestionDto>
        {
            new(1, "What is your age and gender?"),
            new(2, "What is your current running baseline?")
        };
        _gemini.ValidateGoalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(Result<GeminiValidationResult>.Success(new GeminiValidationResult.Valid(validDto)));
        _gemini.GenerateRefinementQuestionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(Result<IReadOnlyList<RefinementQuestionDto>>.Success(questions));

        var result = await _handler.Handle(new TriggerGoalValidationCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value![0].Question.Should().Be("What is your age and gender?");
        session.CurrentStep.Should().Be(OnboardingStep.Step1_RefinementQuestionsActive);
        session.ValidatedGoalJson.Should().NotBeNullOrEmpty();
        session.RefinementQuestionsJson.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidGoal_UpsertCalledMultipleTimes_AtLeastTwice()
    {
        var session = SessionWithDraft("Learn Spanish in 1 year");
        _repository.GetActiveSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _repository.UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>())
                   .Returns(x => x.Arg<OnboardingSession>());

        var validDto = new ValidatedGoalDto("Learn Spanish", "1 year",
            new DateTime(2027, 6, 10, 0, 0, 0, DateTimeKind.Utc), "Intellectual");
        var questions = new List<RefinementQuestionDto> { new(1, "Current level?") };
        _gemini.ValidateGoalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(Result<GeminiValidationResult>.Success(new GeminiValidationResult.Valid(validDto)));
        _gemini.GenerateRefinementQuestionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(Result<IReadOnlyList<RefinementQuestionDto>>.Success(questions));

        await _handler.Handle(new TriggerGoalValidationCommand(), default);

        await _repository.Received(2).UpsertAsync(Arg.Any<OnboardingSession>(), Arg.Any<CancellationToken>());
    }
}
