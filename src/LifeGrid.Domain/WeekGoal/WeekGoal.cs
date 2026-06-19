namespace LifeGrid.Domain.WeekGoal;

public sealed class WeekGoal
{
    private WeekGoal() { }

    public static WeekGoal Create(Guid weekId, Guid goalId) => new()
    {
        WeekGoalId         = Guid.NewGuid(),
        WeekId             = weekId,
        GoalId             = goalId,
        PenaltyState       = PenaltyState.Clean,
        GoalWeeklyGp       = 0.0,
        GoalWeeklyXpEarned = 0
    };

    public Guid         WeekGoalId         { get; private set; }
    public Guid         WeekId             { get; private set; }
    public Guid         GoalId             { get; private set; }
    public PenaltyState PenaltyState       { get; private set; }
    public double       GoalWeeklyGp       { get; private set; }
    public int          GoalWeeklyXpEarned { get; private set; }

    internal void SetGoalWeeklyGp(double value)       => GoalWeeklyGp       = value;
    internal void SetGoalWeeklyXpEarned(int value)    => GoalWeeklyXpEarned = value;
}
