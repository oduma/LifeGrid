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
#if ANDROID
        // Forces the focused EditText to relinquish focus while the DI scope is still live.
        // Without this, a focused Entry fires onFocusChange during Activity.OnDestroy after
        // the scope is disposed, causing a fatal ObjectDisposedException in InputView.MapIsFocused.
        Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.CurrentFocus?.ClearFocus();
#endif
    }
}
