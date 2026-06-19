using LifeGrid.Presentation.ViewModels;

namespace LifeGrid.Presentation.Controls;

public partial class HudView : ContentView
{
    public HudView()
    {
        InitializeComponent();
    }

    private async void OnProfileTapped(object? sender, TappedEventArgs e)
    {
        if (Shell.Current?.BindingContext is not AppShellViewModel { IsOnboardingComplete: true })
            return;
        await Shell.Current.GoToAsync("user-setup");
    }

    private void OnNotificationsTapped(object? sender, TappedEventArgs e) { }
}
