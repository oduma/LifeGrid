using LifeGrid.Application.WeeklyHabits;

namespace LifeGrid.Presentation.ViewModels;

public sealed class WeeklyGoalGroupItem
{
    public WeeklyGoalGroupItem(
        WeeklyGoalGroupDto dto,
        bool               isFuture         = false,
        bool               isCurrentWeek    = false,
        bool               isLoggingEnabled = true,
        bool               hasShields       = false)
    {
        WeekGoalId         = dto.WeekGoalId;
        GoalId             = dto.GoalId;
        GoalDescription    = dto.GoalDescription;
        WeekLabel          = $"Week {dto.WeekGoalNumber}";
        PenaltyState       = dto.PenaltyState;
        GoalWeeklyGp       = dto.GoalWeeklyGp;
        GoalWeeklyXpEarned = dto.GoalWeeklyXpEarned;
        IsInPenalty        = dto.PenaltyState != "Clean";
        IsLevel1Warning    = dto.PenaltyState == "Level1Warning";
        CanUseShield       = IsLevel1Warning && hasShields;
        MetricsText        = $"GP: {dto.GoalWeeklyGp:F2}  XP: {dto.GoalWeeklyXpEarned}";
        CanRequestMomentBurst = isCurrentWeek
            && dto.GoalWeeklyGp >= 100.0
            && !dto.Habits.Any(h => h.HabitType == "MomentBurst");
        Habits             = dto.Habits
            .Select(h => new WeeklyHabitItem(h, dto.GoalDescription, WeekLabel,
                                             isInteractive: !isFuture && isLoggingEnabled))
            .ToList();
    }

    public Guid   WeekGoalId            { get; }
    public Guid   GoalId                { get; }
    public string GoalDescription       { get; }
    public string WeekLabel             { get; }
    public string PenaltyState          { get; }
    public double GoalWeeklyGp          { get; }
    public int    GoalWeeklyXpEarned    { get; }
    public bool   IsInPenalty           { get; }
    public bool   IsLevel1Warning       { get; }
    public bool   CanUseShield          { get; }
    public string MetricsText           { get; }
    public bool   CanRequestMomentBurst { get; }
    public IReadOnlyList<WeeklyHabitItem> Habits { get; }
}
