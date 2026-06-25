using LifeGrid.Application.WeeklyHabits;

namespace LifeGrid.Application.MomentBurst;

public abstract record MomentBurstOutcome
{
    public sealed record HabitCreated(WeeklyHabitItemDto NewHabit) : MomentBurstOutcome;
    public sealed record Denied(string Message) : MomentBurstOutcome;
}
