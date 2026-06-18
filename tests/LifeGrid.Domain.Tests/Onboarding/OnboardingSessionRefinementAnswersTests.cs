using FluentAssertions;
using LifeGrid.Domain.Onboarding;

namespace LifeGrid.Domain.Tests.Onboarding;

public sealed class OnboardingSessionRefinementAnswersTests
{
    // ── SaveRefinementAnswers ────────────────────────────────────────────────

    [Fact]
    public void SaveRefinementAnswers_StoresAnswersJson()
    {
        var session = SessionAtRefinementActive();

        session.SaveRefinementAnswers("[{\"rankOrder\":1,\"answer\":\"5k\"}]");

        session.RefinementAnswersJson.Should().Be("[{\"rankOrder\":1,\"answer\":\"5k\"}]");
    }

    [Fact]
    public void SaveRefinementAnswers_UpdatesLastActiveTimestamp()
    {
        var session = SessionAtRefinementActive();
        var before  = DateTime.UtcNow.AddSeconds(-1);

        session.SaveRefinementAnswers("[]");

        session.LastActiveTimestamp.Should().BeAfter(before);
    }

    [Fact]
    public void SaveRefinementAnswers_DoesNotChangeCurrentStep()
    {
        var session = SessionAtRefinementActive();

        session.SaveRefinementAnswers("[]");

        session.CurrentStep.Should().Be(OnboardingStep.Step1_RefinementQuestionsActive);
    }

    // ── AdvanceToExecutionVerified clears answers ────────────────────────────

    [Fact]
    public void AdvanceToExecutionVerified_ClearsRefinementAnswersJson()
    {
        var session = SessionAtRefinementActive();
        session.SaveRefinementAnswers("[{\"rankOrder\":1,\"answer\":\"5k\"}]");

        session.AdvanceToExecutionVerified();

        session.RefinementAnswersJson.Should().BeNull();
    }

    [Fact]
    public void AdvanceToExecutionVerified_AlsoClearsQuestionsAndValidatedGoal()
    {
        var session = SessionAtRefinementActive();

        session.AdvanceToExecutionVerified();

        session.RefinementQuestionsJson.Should().BeNull();
        session.ValidatedGoalJson.Should().BeNull();
    }

    // ── helper ───────────────────────────────────────────────────────────────

    private static OnboardingSession SessionAtRefinementActive()
    {
        var s = OnboardingSession.Create();
        s.UpdateDraft("Run a marathon");
        s.AdvanceToStep1();
        s.AdvanceToRefinementQuestionsActive(
            "{\"description\":\"Run\",\"ambientTag\":\"Physical\",\"duration\":\"6m\",\"deadlineDate\":\"2026-12-10\"}",
            "[{\"rankOrder\":1,\"question\":\"Baseline?\"}]");
        return s;
    }
}
