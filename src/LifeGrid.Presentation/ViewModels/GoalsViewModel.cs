using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Onboarding.Commands;
using MediatR;
using System.Collections.ObjectModel;

namespace LifeGrid.Presentation.ViewModels;

public partial class GoalsViewModel(IMediator mediator) : ObservableObject
{
    public ObservableCollection<GoalSummaryItem> Goals { get; } = new();

    public async Task LoadAsync()
    {
        var result = await mediator.Send(new GetGoalsQuery());
        if (!result.IsSuccess) return;

        Goals.Clear();
        foreach (var dto in result.Value!)
            Goals.Add(new GoalSummaryItem(
                dto.GoalId,
                dto.Description,
                dto.AmbientTag,
                dto.Duration,
                dto.DeadlineDate,
                dto.Status,
                dto.TotalWeeks));
    }

    [RelayCommand]
    private async Task AddGoalAsync()
    {
        await mediator.Send(new StartNewGoalSessionCommand());
        await Shell.Current.GoToAsync("create-goal");
    }

    [RelayCommand]
    private async Task AbandonGoalSwipeAsync(Guid goalId)
    {
        var xpResult = await mediator.Send(new GetGoalHistoricalXpQuery(goalId));
        if (!xpResult.IsSuccess) return;

        var totalPenalty = xpResult.Value + 100;
        var confirmed    = await Shell.Current.CurrentPage.DisplayAlertAsync(
            "Abandon Goal",
            $"Warning: You will lose {totalPenalty} XP (all XP earned from this goal + 100 XP penalty).",
            "Abandon Forever",
            "Cancel");

        if (!confirmed) return;

        var result = await mediator.Send(new AbandonGoalCommand(goalId));
        if (!result.IsSuccess)
        {
            await Shell.Current.CurrentPage.DisplayAlertAsync("Error", result.Error ?? "Could not abandon goal.", "OK");
            return;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task ExtendScheduleSwipeAsync(Guid goalId)
        => await Shell.Current.GoToAsync($"overwhelmed-recalculate?goalId={goalId}");
}
