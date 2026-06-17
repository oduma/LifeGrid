namespace LifeGrid.Presentation.Controls;

public partial class HudView : ContentView
{
    public HudView()
    {
        InitializeComponent();
    }

    private async void OnProfileTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("setup");

    private void OnNotificationsTapped(object? sender, TappedEventArgs e) { }
}
