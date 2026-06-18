using CommunityToolkit.Mvvm.ComponentModel;

namespace LifeGrid.Presentation.ViewModels;

public sealed partial class RefinementItem : ObservableObject
{
    public int    RankOrder { get; init; }
    public string Question  { get; init; } = string.Empty;

    [ObservableProperty]
    private string _answer = string.Empty;
}
