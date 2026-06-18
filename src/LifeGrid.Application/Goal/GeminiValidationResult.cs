namespace LifeGrid.Application.Goal;

public abstract record GeminiValidationResult
{
    public sealed record Valid(ValidatedGoalDto Data)  : GeminiValidationResult;
    public sealed record Invalid(string RetryPrompt)   : GeminiValidationResult;
}
