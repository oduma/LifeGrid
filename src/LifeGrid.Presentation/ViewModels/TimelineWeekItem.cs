using CommunityToolkit.Mvvm.ComponentModel;

namespace LifeGrid.Presentation.ViewModels;

public partial class TimelineWeekItem : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardBorderState))]
    [NotifyPropertyChangedFor(nameof(CardOpacity))]
    [NotifyPropertyChangedFor(nameof(IsHighlighted))]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusSpText))]
    [NotifyPropertyChangedFor(nameof(CanHibernate))]
    [NotifyPropertyChangedFor(nameof(CanFreeze))]
    private string _status = string.Empty;

    public bool IsCurrentWeek { get; init; }
    public bool IsReEntryWeek { get; init; }

    public Guid       WeekId              { get; init; }
    public DateTime   StartDate           { get; init; }
    public int        TotalWeeklySpEarned { get; init; }
    public IReadOnlyList<TimelineWeekGoalItem> Goals { get; init; } = [];

    public string WeekHeaderText => $"Week of {StartDate:MMM d}";
    public string StatusSpText   => $"{Status} | SP: {TotalWeeklySpEarned}";

    public bool CanHibernate => Status == "Active" && StartDate.Date > DateTime.Today;
    public bool CanFreeze    => Status == "Active" && StartDate.Date <= DateTime.Today &&
                                 DateTime.Today.DayOfWeek < DayOfWeek.Friday;

    public string CardBorderState =>
        IsSelected ? "Selected" : IsCurrentWeek ? "Current" : "Default";

    public double CardOpacity   => IsSelected || IsCurrentWeek ? 1.0 : 0.55;
    public bool   IsHighlighted => IsSelected || IsCurrentWeek;
}
