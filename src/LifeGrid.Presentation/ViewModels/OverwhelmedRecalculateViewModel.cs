using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeGrid.Application.Goal;
using MediatR;

namespace LifeGrid.Presentation.ViewModels;

public partial class OverwhelmedRecalculateViewModel(IMediator mediator)
    : ObservableObject, IQueryAttributable
{
    private Guid _goalId;

    [ObservableProperty]
    private string overwhelmedComment = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("goalId", out var value) &&
            Guid.TryParse(value?.ToString(), out var id))
            _goalId = id;
    }

    [RelayCommand(CanExecute = nameof(CanRecalculate))]
    private async Task RecalculateAsync()
    {
        IsLoading = true;
        RecalculateCommand.NotifyCanExecuteChanged();

        var result = await mediator.Send(
            new RecalculateGoalScheduleCommand(_goalId, OverwhelmedComment));

        IsLoading = false;
        RecalculateCommand.NotifyCanExecuteChanged();

        if (!result.IsSuccess)
        {
            await Shell.Current.CurrentPage.DisplayAlertAsync(
                "Recalculation Failed",
                result.Error ?? "The AI could not generate a feasible new schedule.",
                "OK");
            return;
        }

        await Shell.Current.GoToAsync("../..");
    }

    private bool CanRecalculate() => !IsLoading;
}
