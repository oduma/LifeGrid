namespace LifeGrid.Presentation.ViewModels;

public sealed class TimelineWeekGoalItem(
    string goalDescription,
    string penaltyState,
    double goalWeeklyGp,
    int    goalWeeklyXpEarned)
{
    public string GoalDescription    { get; } = goalDescription;
    public string PenaltyState       { get; } = penaltyState;
    public double GoalWeeklyGp       { get; } = goalWeeklyGp;
    public int    GoalWeeklyXpEarned { get; } = goalWeeklyXpEarned;

    public string MetricsText => $"GP: {GoalWeeklyGp:F2} | XP: {GoalWeeklyXpEarned}";
}
