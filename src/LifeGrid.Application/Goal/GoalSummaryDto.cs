namespace LifeGrid.Application.Goal;

public record GoalSummaryDto(
    Guid     GoalId,
    string   Description,
    string   AmbientTag,
    string   Duration,
    DateTime DeadlineDate,
    string   Status);
