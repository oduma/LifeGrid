using FluentAssertions;
using LifeGrid.Domain.Onboarding;

namespace LifeGrid.Domain.Tests.Onboarding;

public sealed class OnboardingSessionTests
{
    [Fact]
    public void Create_SetsUnstartedStep()
    {
        var session = OnboardingSession.Create();
        session.CurrentStep.Should().Be(OnboardingStep.Unstarted);
    }

    [Fact]
    public void Create_SetsIsCompleteFalse()
    {
        var session = OnboardingSession.Create();
        session.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void Create_SetsNullDraft()
    {
        var session = OnboardingSession.Create();
        session.RawGoalDraft.Should().BeNull();
    }

    [Fact]
    public void Create_AssignsNonEmptySessionId()
    {
        var session = OnboardingSession.Create();
        session.SessionId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_TwoSessions_HaveDifferentIds()
    {
        var a = OnboardingSession.Create();
        var b = OnboardingSession.Create();
        a.SessionId.Should().NotBe(b.SessionId);
    }

    [Fact]
    public void UpdateDraft_StoresDraftText()
    {
        var session = OnboardingSession.Create();
        session.UpdateDraft("run a marathon");
        session.RawGoalDraft.Should().Be("run a marathon");
    }

    [Fact]
    public void UpdateDraft_UpdatesTimestamp()
    {
        var session  = OnboardingSession.Create();
        var before   = session.LastActiveTimestamp;
        session.UpdateDraft("any text");
        session.LastActiveTimestamp.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void UpdateDraft_DoesNotChangeStep()
    {
        var session = OnboardingSession.Create();
        session.UpdateDraft("some text");
        session.CurrentStep.Should().Be(OnboardingStep.Unstarted);
    }

    [Fact]
    public void AdvanceToStep1_SetsStep1Captured()
    {
        var session = OnboardingSession.Create();
        session.AdvanceToStep1();
        session.CurrentStep.Should().Be(OnboardingStep.Step1_GoalDraftCaptured);
    }

    [Fact]
    public void AdvanceToStep1_DoesNotSetIsComplete()
    {
        var session = OnboardingSession.Create();
        session.AdvanceToStep1();
        session.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void AdvanceToStep1_UpdatesTimestamp()
    {
        var session = OnboardingSession.Create();
        var before  = session.LastActiveTimestamp;
        session.AdvanceToStep1();
        session.LastActiveTimestamp.Should().BeOnOrAfter(before);
    }
}
