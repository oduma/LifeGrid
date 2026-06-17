using CommunityToolkit.Mvvm.ComponentModel;

namespace LifeGrid.Presentation.ViewModels;

public partial class AppShellViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isOnboardingComplete = false;

    [ObservableProperty]
    private bool _isProfileActive = false;
}
