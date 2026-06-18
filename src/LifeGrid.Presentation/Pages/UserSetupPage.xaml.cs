using LifeGrid.Presentation.ViewModels;

namespace LifeGrid.Presentation.Pages;

public partial class UserSetupPage : ContentPage
{
    private readonly UserSetupViewModel _vm;

    public UserSetupPage(UserSetupViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
