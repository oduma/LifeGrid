using FluentAssertions;
using LifeGrid.Domain.Onboarding;

namespace LifeGrid.Domain.Tests.Onboarding;

public sealed class OnboardingSessionFactoryResetTests
{
    // ── helper ───────────────────────────────────────────────────────────────

    private static OnboardingSession SessionAtHabitsGenerated()
    {
        var s = OnboardingSession.Create();
        s.UpdateDraft("Run a marathon");
        s.AdvanceToStep1();
        s.LinkToUser(Guid.NewGuid());
        s.AdvanceToAwaitingValidation();
        s.AdvanceToRefinementQuestionsActive(
            "{\"description\":\"Run\"}",
            "[{\"rankOrder\":1,\"question\":\"Baseline?\"}]");
        s.SaveRefinementAnswers("[{\"rankOrder\":1,\"answer\":\"5k\"}]");
        s.AdvanceToExecutionVerified();
        s.AdvanceToHabitsGenerated();
        return s;
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_SetsCurrentStepToUnstarted()
    {
        var session = SessionAtHabitsGenerated();

        session.Reset();

        session.CurrentStep.Should().Be(OnboardingStep.Unstarted);
    }

    [Fact]
    public void Reset_ClearsIsComplete()
    {
        var session = SessionAtHabitsGenerated();

        session.Reset();

        session.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void Reset_ClearsUserId()
    {
        var session = SessionAtHabitsGenerated();

        session.Reset();

        session.UserId.Should().BeNull();
    }

    [Fact]
    public void Reset_ClearsAllStagingFields()
    {
        var session = SessionAtHabitsGenerated();

        session.Reset();

        session.RawGoalDraft.Should().BeNull();
        session.ValidatedGoalJson.Should().BeNull();
        session.RefinementQuestionsJson.Should().BeNull();
        session.RefinementAnswersJson.Should().BeNull();
    }

    [Fact]
    public void Reset_UpdatesLastActiveTimestamp()
    {
        var session = SessionAtHabitsGenerated();
        var before  = session.LastActiveTimestamp;

        session.Reset();

        session.LastActiveTimestamp.Should().BeOnOrAfter(before);
    }
}
