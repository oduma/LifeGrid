namespace LifeGrid.Application.Week;

public abstract record HabitSchedulingResult
{
    public sealed record Feasible(IReadOnlyList<WeekScheduleDto> Schedule) : HabitSchedulingResult;

    public sealed record Infeasible(
        string  RecalibrationReason,
        string? SuggestedDeadline,
        string? SuggestedAlternativeScope) : HabitSchedulingResult;
}
