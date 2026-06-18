using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeGrid.Application.Goal;
using LifeGrid.Application.UserSetup.Commands;
using MediatR;

namespace LifeGrid.Presentation.ViewModels;

public partial class UserSetupViewModel(IMediator mediator, AppShellViewModel appShellViewModel)
    : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWarning))]
    private bool _hasActiveGoals = false;

    public bool ShowWarning => HasActiveGoals;

    public async Task LoadAsync()
    {
        var result = await mediator.Send(new GetActiveGoalCountQuery());
        if (result.IsSuccess)
            HasActiveGoals = result.Value > 0;
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
    private void DetectHiddenVices() { }
}
