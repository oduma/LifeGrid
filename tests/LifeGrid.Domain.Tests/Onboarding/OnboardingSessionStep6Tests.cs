using FluentAssertions;
using LifeGrid.Domain.Onboarding;

namespace LifeGrid.Domain.Tests.Onboarding;

public sealed class OnboardingSessionStep6Tests
{
    [Fact]
    public void AdvanceToHabitsGenerated_SetsIsCompleteTrue()
    {
        var session = SessionAtExecutionVerified();

        session.AdvanceToHabitsGenerated();

        session.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void AdvanceToHabitsGenerated_SetsCorrectStep()
    {
        var session = SessionAtExecutionVerified();

        session.AdvanceToHabitsGenerated();

        session.CurrentStep.Should().Be(OnboardingStep.Step6_HabitsGenerated);
    }

    [Fact]
    public void AdvanceToHabitsGenerated_UpdatesLastActiveTimestamp()
    {
        var session = SessionAtExecutionVerified();
        var before  = DateTime.UtcNow.AddSeconds(-1);

        session.AdvanceToHabitsGenerated();

        session.LastActiveTimestamp.Should().BeAfter(before);
    }

    // ── helper ───────────────────────────────────────────────────────────────

    private static OnboardingSession SessionAtExecutionVerified()
    {
        var s = OnboardingSession.Create();
        s.UpdateDraft("Run a marathon");
        s.AdvanceToStep1();
        s.AdvanceToRefinementQuestionsActive("{}", "[]");
        s.AdvanceToExecutionVerified();
        return s;
    }
}
