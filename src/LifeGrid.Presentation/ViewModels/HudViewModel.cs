using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.Hud;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LifeGrid.Presentation.ViewModels;

public partial class HudViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppShellViewModel    _appShell;

    public HudViewModel(IServiceScopeFactory scopeFactory, AppShellViewModel appShellViewModel)
    {
        _scopeFactory = scopeFactory;
        _appShell     = appShellViewModel;
        _appShell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppShellViewModel.IsProfileActive))
                OnPropertyChanged(nameof(IsProfileActive));
        };
        WeakReferenceMessenger.Default.Register<HudViewModel, EconomyStateMutatedMessage>(this,
            async (r, _) => await r.LoadAsync());
    }

    // Passthrough — keeps the existing DataTrigger in HudView.xaml working
    // after BindingContext changes from AppShellViewModel to HudViewModel.
    public bool IsProfileActive => _appShell.IsProfileActive;

    [ObservableProperty] private string _level         = "0";
    [ObservableProperty] private string _gpLifetime    = "0";
    [ObservableProperty] private string _gpWeekly      = "0";
    [ObservableProperty] private string _xpLifetime    = "0";
    [ObservableProperty] private string _xpWeekly      = "0";
    [ObservableProperty] private string _spCurrent     = "0";
    [ObservableProperty] private string _spWeekly      = "0";
    [ObservableProperty] private string _shieldsActive = "0";
    [ObservableProperty] private string _shieldsCap    = "0";

    public async Task LoadAsync(CancellationToken ct = default)
    {
        using var scope   = _scopeFactory.CreateScope();
        var       mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new GetHudTelemetryQuery(), ct);
        if (!result.IsSuccess) return;
        var d = result.Value!;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Level         = d.Level.ToString();
            GpLifetime    = ((int)Math.Ceiling(d.LifetimeGp)).ToString();
            GpWeekly      = ((int)Math.Ceiling(d.WeeklyGp)).ToString();
            XpLifetime    = d.LifetimeXp.ToString();
            XpWeekly      = d.WeeklyXp.ToString();
            SpCurrent     = d.CurrentSp.ToString();
            SpWeekly      = d.WeeklySpEarned.ToString();
            ShieldsActive = d.ActiveShields.ToString();
            ShieldsCap    = d.ShieldCap.ToString();
        });
    }
}
