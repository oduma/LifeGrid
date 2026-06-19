using LifeGrid.Presentation.ViewModels;

namespace LifeGrid.Presentation.Pages;

public partial class ViceSurveyPage : ContentPage
{
    private readonly ViceSurveyViewModel _viewModel;

    public ViceSurveyPage(ViceSurveyViewModel viewModel)
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
