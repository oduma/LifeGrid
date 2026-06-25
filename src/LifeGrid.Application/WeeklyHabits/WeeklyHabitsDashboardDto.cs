namespace LifeGrid.Application.WeeklyHabits;

public record WeeklyHabitsDashboardDto(
    Guid     WeekId,
    DateTime StartDate,
    string   Status,
    int      TotalWeeklySpEarned,
    IReadOnlyList<WeeklyGoalGroupDto> GoalGroups);

public record WeeklyGoalGroupDto(
    Guid   WeekGoalId,
    Guid   GoalId,
    string GoalDescription,
    int    WeekGoalNumber,
    string PenaltyState,
    double GoalWeeklyGp,
    int    GoalWeeklyXpEarned,
    IReadOnlyList<WeeklyHabitItemDto> Habits);

public record WeeklyHabitItemDto(
    Guid     HabitId,
    string   HabitType,
    string   HabitName,
    string   HabitDescription,
    double   TargetValue,
    string   MeasurementUnit,
    DateTime DeadlineDateTime,
    IReadOnlyList<HabitCompletionLogDto> CompletionLogs);

public record HabitCompletionLogDto(
    Guid     LogId,
    double   ActualValue,
    string   MeasurementUnit,
    string?  ProofText,
    string?  ProofImageUrl,
    DateTime Timestamp);
