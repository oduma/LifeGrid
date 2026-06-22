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

        if (_viewModel.ActiveWeekIndex >= 0)
            WeeksCollection.ScrollTo(
                _viewModel.ActiveWeekIndex,
                position: ScrollToPosition.Center,
                animate: false);
    }

    private void OnCollectionViewScrolled(object? sender, ItemsViewScrolledEventArgs e)
        => _viewModel.SetActiveWeekByIndex(e.CenterItemIndex);
}
