using CommunityToolkit.Mvvm.ComponentModel;

namespace LifeGrid.Presentation.ViewModels;

public partial class TimelineWeekItem : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardBorderState))]
    [NotifyPropertyChangedFor(nameof(CardOpacity))]
    [NotifyPropertyChangedFor(nameof(IsHighlighted))]
    private bool _isSelected;

    public bool IsCurrentWeek { get; init; }

    public Guid       WeekId              { get; init; }
    public DateTime   StartDate           { get; init; }
    public string     Status              { get; init; } = string.Empty;
    public int        TotalWeeklySpEarned { get; init; }
    public IReadOnlyList<TimelineWeekGoalItem> Goals { get; init; } = [];

    public string WeekHeaderText => $"Week of {StartDate:MMM d}";
    public string StatusSpText   => $"{Status} | SP: {TotalWeeklySpEarned}";

    public string CardBorderState =>
        IsSelected ? "Selected" : IsCurrentWeek ? "Current" : "Default";

    public double CardOpacity   => IsSelected || IsCurrentWeek ? 1.0 : 0.55;
    public bool   IsHighlighted => IsSelected || IsCurrentWeek;
}
