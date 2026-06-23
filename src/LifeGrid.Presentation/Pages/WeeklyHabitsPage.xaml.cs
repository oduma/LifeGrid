using LifeGrid.Presentation.ViewModels;

namespace LifeGrid.Presentation.Pages;

public partial class WeeklyHabitsPage : ContentPage
{
    private readonly WeeklyHabitsViewModel _viewModel;

    public WeeklyHabitsPage(WeeklyHabitsViewModel viewModel)
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
