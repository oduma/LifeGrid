namespace LifeGrid.Application.Goal;

public sealed record ValidatedGoalDto(
    string   Description,
    string   Duration,
    DateTime DeadlineDate,
    string   AmbientTag);
