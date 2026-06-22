namespace LifeGrid.Application.Timeline;

public record TimelineWeekGoalDto(
    string GoalDescription,
    string PenaltyState,
    double GoalWeeklyGp,
    int    GoalWeeklyXpEarned);
