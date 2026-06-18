using LifeGrid.Application.Goal;
using LifeGrid.Application.Onboarding.Queries;
using LifeGrid.Application.UserProfile.Queries;
using LifeGrid.Infrastructure.Security;
using LifeGrid.Presentation.ViewModels;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LifeGrid.Presentation;

public partial class App
{
    private readonly IServiceProvider          _services;
    private readonly IMediator                 _mediator;
    private readonly IApiCredentialSyncService _credentialSync;
    private readonly AppShellViewModel         _appShellViewModel;

    public App(
        IServiceProvider          services,
        IMediator                 mediator,
        IApiCredentialSyncService credentialSync,
        AppShellViewModel         appShellViewModel)
    {
        InitializeComponent();
        UserAppTheme       = AppTheme.Light;
        _services          = services;
        _mediator          = mediator;
        _credentialSync    = credentialSync;
        _appShellViewModel = appShellViewModel;
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new Window(_services.GetRequiredService<AppShell>());

    protected override async void OnStart()
    {
        base.OnStart();
        await _credentialSync.SyncAsync();
        await _mediator.Send(new GetOrCreateUserProfileQuery());

        // Returning user: active goals already exist — enable tabs and open Goals
        var countResult = await _mediator.Send(new GetActiveGoalCountQuery());
        if (countResult.IsSuccess && countResult.Value > 0)
        {
            _appShellViewModel.SetOnboardingComplete();
            await Shell.Current.GoToAsync("//goals");
            return;
        }

        // New or mid-onboarding user
        var sessionResult = await _mediator.Send(new GetOrCreateOnboardingSessionQuery());
        if (sessionResult.IsSuccess && !sessionResult.Value!.IsComplete)
            await Shell.Current.GoToAsync("create-goal");
    }
}
