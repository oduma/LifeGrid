using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LifeGrid.Presentation.ViewModels;

public partial class OptionItemViewModel : ObservableObject
{
    private readonly Action<OptionItemViewModel> _onSelected;

    public OptionItemViewModel(string text, Action<OptionItemViewModel> onSelected)
    {
        Text        = text;
        _onSelected = onSelected;
        SelectCommand = new RelayCommand(() => _onSelected(this));
    }

    [ObservableProperty]
    private bool _isSelected;

    public string        Text          { get; }
    public IRelayCommand SelectCommand { get; }
}
