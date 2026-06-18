using LifeGrid.Presentation.ViewModels;

namespace LifeGrid.Presentation.Pages;

public partial class CreateGoalPage : ContentPage
{
    private readonly CreateGoalViewModel  _vm;
    private readonly AppShellViewModel    _shellVm;

    public CreateGoalPage(CreateGoalViewModel vm, AppShellViewModel shellVm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _shellVm              = shellVm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _shellVm.IsProfileActive = true;
        await _vm.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _shellVm.IsProfileActive = false;
    }
}
