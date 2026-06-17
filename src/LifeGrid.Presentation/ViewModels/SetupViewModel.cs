using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeGrid.Application.Onboarding.Commands;
using LifeGrid.Application.Onboarding.Queries;
using LifeGrid.Domain.Onboarding;
using MediatR;

namespace LifeGrid.Presentation.ViewModels;

public partial class SetupViewModel(IMediator mediator) : ObservableObject
{
    private CancellationTokenSource? _debounceCts;

    [ObservableProperty]
    private string _goalDraft = string.Empty;

    [ObservableProperty]
    private bool _isStep1Complete = false;

    public async Task LoadAsync()
    {
        var result = await mediator.Send(new GetOrCreateOnboardingSessionQuery());
        if (!result.IsSuccess) return;

        var session = result.Value!;
        GoalDraft       = session.RawGoalDraft ?? string.Empty;
        IsStep1Complete = session.CurrentStep == OnboardingStep.Step1_GoalDraftCaptured;
    }

    partial void OnGoalDraftChanged(string value) => ScheduleAutoSave(value);

    private void ScheduleAutoSave(string draft)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Delay(500, token).ContinueWith(
            async _ => await mediator.Send(new UpdateGoalDraftCommand(draft)),
            token,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);
    }

    [RelayCommand]
    private async Task CompleteStep1Async()
    {
        _debounceCts?.Cancel();
        await mediator.Send(new UpdateGoalDraftCommand(GoalDraft));
        var result = await mediator.Send(new CompleteStep1Command());
        if (result.IsSuccess)
            IsStep1Complete = true;
    }
}
