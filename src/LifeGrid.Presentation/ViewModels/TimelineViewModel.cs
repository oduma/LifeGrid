using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeGrid.Application.Timeline;
using MediatR;
using System.Collections.ObjectModel;
using GoalAggregate = LifeGrid.Domain.Goal.Goal;

namespace LifeGrid.Presentation.ViewModels;

public partial class TimelineViewModel(IMediator mediator) : ObservableObject, IQueryAttributable
{
    private int               _currentWeekIndex = -1;
    private TimelineWeekItem? _selectedWeek;
    private IReadOnlyList<Guid>? _filterGoalIds;

    public ObservableCollection<TimelineWeekItem> Weeks { get; } = new();

    public int CurrentWeekIndex => _currentWeekIndex;

    [ObservableProperty]
    private bool _isFilteredMode;

    // Shell calls ApplyQueryAttributes AFTER OnAppearing for tab navigation.
    // OnAppearing loads with the previous filter state; this method corrects it.
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("filterGoalIds", out var value) &&
            value is IReadOnlyList<Guid> { Count: > 0 } ids)
        {
            _filterGoalIds = ids;
            IsFilteredMode = true;
        }
        else
        {
            _filterGoalIds = null;
            IsFilteredMode = false;
        }
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task SeeAllGoalsAsync()
    {
        _filterGoalIds = null;
        IsFilteredMode = false;
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        var result = await mediator.Send(new GetTimelineQuery(_filterGoalIds));
        if (!result.IsSuccess) return;

        Weeks.Clear();
        _currentWeekIndex = -1;
        _selectedWeek     = null;

        var currentMonday = GoalAggregate.CalculateStartDate(DateTime.Today);

        foreach (var dto in result.Value!)
        {
            Weeks.Add(new TimelineWeekItem
            {
                WeekId              = dto.WeekId,
                StartDate           = dto.StartDate,
                Status              = dto.Status,
                TotalWeeklySpEarned = dto.TotalWeeklySpEarned,
                IsCurrentWeek       = dto.StartDate.Date == currentMonday.Date,
                Goals               = dto.Goals.Select(g => new TimelineWeekGoalItem(
                    g.GoalDescription,
                    g.PenaltyState,
                    g.GoalWeeklyGp,
                    g.GoalWeeklyXpEarned)).ToList()
            });
        }

        for (int i = 0; i < Weeks.Count; i++)
        {
            if (Weeks[i].IsCurrentWeek)
            {
                _currentWeekIndex = i;
                break;
            }
        }

        var initialIndex = _currentWeekIndex >= 0 ? _currentWeekIndex : Weeks.Count - 1;
        if (initialIndex >= 0)
        {
            _selectedWeek = Weeks[initialIndex];
            _selectedWeek.IsSelected = true;
        }
    }

    [RelayCommand]
    private void SelectWeek(TimelineWeekItem item)
    {
        if (_selectedWeek == item) return;
        if (_selectedWeek is not null) _selectedWeek.IsSelected = false;
        _selectedWeek = item;
        item.IsSelected = true;
    }
}
