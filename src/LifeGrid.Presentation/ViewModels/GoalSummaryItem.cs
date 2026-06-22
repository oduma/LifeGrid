using CommunityToolkit.Mvvm.ComponentModel;

namespace LifeGrid.Presentation.ViewModels;

public sealed partial class GoalSummaryItem : ObservableObject
{
    public required Guid     GoalId       { get; init; }
    public required string   Description  { get; init; }
    public required string   AmbientTag   { get; init; }
    public required string   Duration     { get; init; }
    public required DateTime DeadlineDate { get; init; }
    public required string   Status       { get; init; }
    public required int      TotalWeeks   { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}
