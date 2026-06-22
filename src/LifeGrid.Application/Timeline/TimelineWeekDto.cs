namespace LifeGrid.Application.Timeline;

public record TimelineWeekDto(
    Guid                               WeekId,
    DateTime                           StartDate,
    string                             Status,
    int                                TotalWeeklySpEarned,
    IReadOnlyList<TimelineWeekGoalDto> Goals);
