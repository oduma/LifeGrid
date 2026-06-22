using LifeGrid.Presentation.ViewModels;

namespace LifeGrid.Presentation.Pages;

public partial class TimelinePage : ContentPage
{
    private readonly TimelineViewModel _viewModel;

    public TimelinePage(TimelineViewModel viewModel)
    {
        InitializeComponent();
        _viewModel     = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();

        if (_viewModel.CurrentWeekIndex >= 0)
            WeeksCollection.ScrollTo(
                _viewModel.CurrentWeekIndex,
                position: ScrollToPosition.Center,
                animate: false);
    }
}
