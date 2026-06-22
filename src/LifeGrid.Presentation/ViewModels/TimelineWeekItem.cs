using CommunityToolkit.Mvvm.ComponentModel;

namespace LifeGrid.Presentation.ViewModels;

public partial class TimelineWeekItem : ObservableObject
{
    [ObservableProperty]
    private bool isActive;

    public Guid       WeekId              { get; init; }
    public DateTime   StartDate           { get; init; }
    public string     Status              { get; init; } = string.Empty;
    public int        TotalWeeklySpEarned { get; init; }
    public IReadOnlyList<TimelineWeekGoalItem> Goals { get; init; } = [];

    public string WeekHeaderText => $"Week of {StartDate:MMM d}";
    public string StatusSpText   => $"{Status} | SP: {TotalWeeklySpEarned}";
}
