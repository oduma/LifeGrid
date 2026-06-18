using FluentAssertions;
using LifeGrid.Domain.Onboarding;

namespace LifeGrid.Domain.Tests.Onboarding;

public sealed class OnboardingSessionStep5Tests
{
    [Fact]
    public void AdvanceToAwaitingValidation_SetsCorrectStep()
    {
        var session = OnboardingSession.Create();
        session.AdvanceToStep1();

        session.AdvanceToAwaitingValidation();

        session.CurrentStep.Should().Be(OnboardingStep.Step1_AwaitingValidation);
    }

    [Fact]
    public void AdvanceToAwaitingValidation_UpdatesLastActiveTimestamp()
    {
        var session = OnboardingSession.Create();
        var before  = DateTime.UtcNow.AddSeconds(-1);

        session.AdvanceToAwaitingValidation();

        session.LastActiveTimestamp.Should().BeAfter(before);
    }

    [Fact]
    public void AdvanceToRefinementQuestionsActive_SetsStepAndStoresJson()
    {
        var session = OnboardingSession.Create();
        session.AdvanceToAwaitingValidation();

        session.AdvanceToRefinementQuestionsActive("{\"goal\":\"test\"}", "[{\"RankOrder\":1}]");

        session.CurrentStep.Should().Be(OnboardingStep.Step1_RefinementQuestionsActive);
        session.ValidatedGoalJson.Should().Be("{\"goal\":\"test\"}");
        session.RefinementQuestionsJson.Should().Be("[{\"RankOrder\":1}]");
    }

    [Fact]
    public void AdvanceToExecutionVerified_SetsStepAndClearsStagingFields()
    {
        var session = OnboardingSession.Create();
        session.AdvanceToRefinementQuestionsActive("{}", "[]");

        session.AdvanceToExecutionVerified();

        session.CurrentStep.Should().Be(OnboardingStep.Step1_ExecutionVerified);
        session.ValidatedGoalJson.Should().BeNull();
        session.RefinementQuestionsJson.Should().BeNull();
    }

    [Fact]
    public void RevertToGoalDraftCaptured_ResetsStep()
    {
        var session = OnboardingSession.Create();
        session.AdvanceToAwaitingValidation();

        session.RevertToGoalDraftCaptured();

        session.CurrentStep.Should().Be(OnboardingStep.Step1_GoalDraftCaptured);
    }

    [Fact]
    public void RevertToGoalDraftCaptured_DoesNotClearStagingFields()
    {
        var session = OnboardingSession.Create();
        session.AdvanceToRefinementQuestionsActive("{}", "[]");

        session.RevertToGoalDraftCaptured();

        session.ValidatedGoalJson.Should().Be("{}");
    }
}
