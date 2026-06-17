using LifeGrid.Application.Onboarding.Queries;
using LifeGrid.Infrastructure.Security;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LifeGrid.Presentation;

public partial class App
{
    private readonly IServiceProvider        _services;
    private readonly IMediator               _mediator;
    private readonly IApiCredentialSyncService _credentialSync;

    public App(IServiceProvider services, IMediator mediator, IApiCredentialSyncService credentialSync)
    {
        InitializeComponent();          // loads App.xaml → Colors.xaml + Styles.xaml
        UserAppTheme = AppTheme.Light;
        _services        = services;
        _mediator        = mediator;
        _credentialSync  = credentialSync;
    }

    protected override Window CreateWindow(IActivationState? activationState)
        // AppShell is resolved here — after InitializeComponent() — so StaticResources are available
        => new Window(_services.GetRequiredService<AppShell>());

    protected override async void OnStart()
    {
        base.OnStart();
        await _credentialSync.SyncAsync();
        var result = await _mediator.Send(new GetOrCreateOnboardingSessionQuery());
        if (result.IsSuccess && !result.Value!.IsComplete)
            await Shell.Current.GoToAsync("setup");
    }
}
