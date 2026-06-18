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
                dto.Status));
    }

    [RelayCommand]
    private async Task AddGoalAsync()
    {
        await mediator.Send(new StartNewGoalSessionCommand());
        await Shell.Current.GoToAsync("setup");
    }
}
