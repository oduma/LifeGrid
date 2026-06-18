namespace LifeGrid.Presentation.ViewModels;

public record GoalSummaryItem(
    Guid     GoalId,
    string   Description,
    string   AmbientTag,
    string   Duration,
    DateTime DeadlineDate,
    string   Status);
