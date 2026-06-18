namespace LifeGrid.Application.Week;

public abstract record HabitGenerationOutcome
{
    public sealed record Complete : HabitGenerationOutcome;

    public sealed record Infeasible(
        string  RecalibrationReason,
        string? SuggestedDeadline,
        string? SuggestedAlternativeScope) : HabitGenerationOutcome;
}
