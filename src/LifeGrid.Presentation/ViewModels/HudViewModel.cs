using CommunityToolkit.Mvvm.ComponentModel;
using LifeGrid.Application.Hud;
using MediatR;

namespace LifeGrid.Presentation.ViewModels;

public partial class HudViewModel : ObservableObject
{
    private readonly IMediator         _mediator;
    private readonly AppShellViewModel _appShell;

    public HudViewModel(IMediator mediator, AppShellViewModel appShellViewModel)
    {
        _mediator = mediator;
        _appShell = appShellViewModel;
        _appShell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppShellViewModel.IsProfileActive))
                OnPropertyChanged(nameof(IsProfileActive));
        };
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
        var result = await _mediator.Send(new GetHudTelemetryQuery(), ct);
        if (!result.IsSuccess) return;
        var d      = result.Value!;
        Level         = d.Level.ToString();
        GpLifetime    = ((int)Math.Ceiling(d.LifetimeGp)).ToString();
        GpWeekly      = ((int)Math.Ceiling(d.WeeklyGp)).ToString();
        XpLifetime    = d.LifetimeXp.ToString();
        XpWeekly      = d.WeeklyXp.ToString();
        SpCurrent     = d.CurrentSp.ToString();
        SpWeekly      = d.WeeklySpEarned.ToString();
        ShieldsActive = d.ActiveShields.ToString();
        ShieldsCap    = d.ShieldCap.ToString();
    }
}
