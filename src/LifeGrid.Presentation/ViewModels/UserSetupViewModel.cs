using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeGrid.Application.Goal;
using LifeGrid.Application.UserSetup.Commands;
using LifeGrid.Application.Vice;
using MediatR;

namespace LifeGrid.Presentation.ViewModels;

public partial class UserSetupViewModel(IMediator mediator, AppShellViewModel appShellViewModel)
    : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWarning))]
    private bool _hasActiveGoals = false;

    public bool ShowWarning => HasActiveGoals;

    [ObservableProperty] private bool _isViceSurveyAvailable = true;

    public async Task LoadAsync()
    {
        var result = await mediator.Send(new GetActiveGoalCountQuery());
        if (result.IsSuccess)
            HasActiveGoals = result.Value > 0;

        var surveyCheck = await mediator.Send(new LaunchViceSurveyCommand());
        IsViceSurveyAvailable = surveyCheck.IsSuccess;
    }

    [RelayCommand]
    private async Task EditActiveGoalsAsync()
        => await Shell.Current.GoToAsync("//goals");

    [RelayCommand]
    private async Task ResetGoalsAsync()
    {
        await mediator.Send(new FactoryResetCommand());
        appShellViewModel.IsOnboardingComplete = false;
        await Shell.Current.GoToAsync("create-goal");
    }

    [RelayCommand]
    private static async Task DetectHiddenVicesAsync()
        => await Shell.Current.GoToAsync("vice-survey");
}
