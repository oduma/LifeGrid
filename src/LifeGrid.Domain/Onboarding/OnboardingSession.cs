namespace LifeGrid.Domain.Onboarding;

public sealed class OnboardingSession
{
    private OnboardingSession() { }

    public static OnboardingSession Create() => new()
    {
        SessionId           = Guid.NewGuid(),
        CurrentStep         = OnboardingStep.Unstarted,
        IsComplete          = false,
        RawGoalDraft        = null,
        LastActiveTimestamp = DateTime.UtcNow
    };

    public Guid           SessionId           { get; private set; }
    public OnboardingStep CurrentStep         { get; private set; }
    public bool           IsComplete          { get; private set; }
    public string?        RawGoalDraft        { get; private set; }
    public DateTime       LastActiveTimestamp { get; private set; }

    public void UpdateDraft(string draft)
    {
        RawGoalDraft        = draft;
        LastActiveTimestamp = DateTime.UtcNow;
    }

    public void AdvanceToStep1()
    {
        CurrentStep         = OnboardingStep.Step1_GoalDraftCaptured;
        LastActiveTimestamp = DateTime.UtcNow;
    }

    public Guid? UserId { get; private set; }

    public void LinkToUser(Guid userId) => UserId = userId;
}
