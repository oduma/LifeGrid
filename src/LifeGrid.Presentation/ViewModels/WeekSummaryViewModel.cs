using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeGrid.Application.Week;
using LifeGrid.Application.WeeklyHabits;
using MediatR;
using System.Collections.ObjectModel;

namespace LifeGrid.Presentation.ViewModels;

public partial class WeekSummaryViewModel : ObservableObject, IQueryAttributable
{
    private readonly IMediator _mediator;
    private Guid _weekId;

    public WeekSummaryViewModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    [ObservableProperty] private string _weekHeaderText      = string.Empty;
    [ObservableProperty] private string _weekStatusText      = string.Empty;
    [ObservableProperty] private bool   _hasShieldsAvailable;

    public ObservableCollection<WeeklyGoalGroupItem> GoalGroups { get; } = new();

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("weekId", out var wid))
        {
            if (wid is Guid g)                                             _weekId = g;
            else if (wid is string s && Guid.TryParse(s, out var parsed)) _weekId = parsed;
        }
    }

    public async Task LoadAsync()
    {
        var result = await _mediator.Send(new GetWeeklyHabitsQuery(_weekId, null));
        if (!result.IsSuccess) return;

        var dto = result.Value!;
        WeekHeaderText      = dto.StartDate.ToString("MMM dd, yyyy");
        WeekStatusText      = $"{dto.Status}  |  SP: {dto.TotalWeeklySpEarned}";
        HasShieldsAvailable = dto.ShieldsAvailable > 0;

        GoalGroups.Clear();
        foreach (var g in dto.GoalGroups)
            GoalGroups.Add(new WeeklyGoalGroupItem(g,
                isFuture: false, isCurrentWeek: false, isLoggingEnabled: false,
                hasShields: dto.ShieldsAvailable > 0));
    }

    [RelayCommand]
    private async Task UseShieldAsync(WeeklyGoalGroupItem item)
    {
        var result = await _mediator.Send(new UseShieldCommand(item.WeekGoalId));
        if (result.IsSuccess)
            await LoadAsync();
    }
}
