using LifeGrid.Presentation.ViewModels;

namespace LifeGrid.Presentation.Pages;

public partial class WeekSummaryPage : ContentPage
{
    private readonly WeekSummaryViewModel _viewModel;

    public WeekSummaryPage(WeekSummaryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel     = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
