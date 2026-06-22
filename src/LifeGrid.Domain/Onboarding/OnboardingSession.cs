namespace LifeGrid.Domain.Onboarding;

public sealed class OnboardingSession
{
    private OnboardingSession() { }

    public static OnboardingSession Create() => new()
    {
        SessionId           = Guid.NewGuid(),
        CurrentStep         = OnboardingStep.Unstarted,
        RawGoalDraft        = null,
        LastActiveTimestamp = DateTime.UtcNow
    };

    public Guid           SessionId           { get; private set; }
    public OnboardingStep CurrentStep         { get; private set; }
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

    public Guid?     UserId                  { get; private set; }
    public string?   ValidatedGoalJson       { get; private set; }
    public string?   RefinementQuestionsJson { get; private set; }
    public string?   RefinementAnswersJson   { get; private set; }
    public DateTime? ChosenStartDate         { get; private set; }
    public string?   BlueprintJson           { get; private set; }
    public Guid?     GoalId                  { get; private set; }

    public void LinkToUser(Guid userId) => UserId = userId;

    public void SetChosenStartDate(DateTime startDate) => ChosenStartDate = startDate;

    public void AdvanceToAwaitingValidation()
    {
        CurrentStep         = OnboardingStep.Step1_AwaitingValidation;
        LastActiveTimestamp = DateTime.UtcNow;
    }

    public void AdvanceToRefinementQuestionsActive(string validatedGoalJson, string refinementQuestionsJson)
    {
        ValidatedGoalJson       = validatedGoalJson;
        RefinementQuestionsJson = refinementQuestionsJson;
        CurrentStep             = OnboardingStep.Step1_RefinementQuestionsActive;
        LastActiveTimestamp     = DateTime.UtcNow;
    }

    public void SaveRefinementAnswers(string answersJson)
    {
        RefinementAnswersJson = answersJson;
        LastActiveTimestamp   = DateTime.UtcNow;
    }

    public void SetGoal(Guid goalId)
    {
        GoalId              = goalId;
        LastActiveTimestamp = DateTime.UtcNow;
    }

    public void CacheBlueprint(string blueprintJson)
    {
        BlueprintJson       = blueprintJson;
        LastActiveTimestamp = DateTime.UtcNow;
    }

    public void AdvanceToExecutionVerified()
    {
        ValidatedGoalJson       = null;
        RefinementQuestionsJson = null;
        RefinementAnswersJson   = null;
        CurrentStep             = OnboardingStep.Step1_ExecutionVerified;
        LastActiveTimestamp     = DateTime.UtcNow;
    }

    public void RevertToGoalDraftCaptured()
    {
        CurrentStep         = OnboardingStep.Step1_GoalDraftCaptured;
        LastActiveTimestamp = DateTime.UtcNow;
    }
}
