using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.Timeline;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Week;
using MediatR;
using System.Collections.ObjectModel;

namespace LifeGrid.Presentation.ViewModels;

public partial class TimelineViewModel : ObservableObject, IQueryAttributable
{
    private readonly IMediator _mediator;

    private int               _currentWeekIndex = -1;
    private TimelineWeekItem? _selectedWeek;
    private IReadOnlyList<Guid>? _filterGoalIds;

    public TimelineViewModel(IMediator mediator)
    {
        _mediator = mediator;
        WeakReferenceMessenger.Default.Register<TimelineViewModel, EconomyStateMutatedMessage>(this,
            async (r, _) => await r.LoadAsync());
    }

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
        var result = await _mediator.Send(new GetTimelineQuery(_filterGoalIds));
        if (!result.IsSuccess) return;

        Weeks.Clear();
        _currentWeekIndex = -1;
        _selectedWeek     = null;

        var today         = DateTime.Today;
        int daysFromMon   = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var currentMonday = today.AddDays(-daysFromMon);

        foreach (var dto in result.Value!)
        {
            Weeks.Add(new TimelineWeekItem
            {
                WeekId              = dto.WeekId,
                StartDate           = dto.StartDate,
                Status              = dto.Status,
                TotalWeeklySpEarned = dto.TotalWeeklySpEarned,
                IsCurrentWeek       = dto.StartDate.Date == currentMonday.Date,
                IsReEntryWeek       = dto.IsReEntryWeek,
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

    [RelayCommand]
    private async Task DrillDownToWeekAsync(TimelineWeekItem item)
    {
        var parameters = new ShellNavigationQueryParameters
        {
            ["weekId"] = item.WeekId
        };
        if (_filterGoalIds is { Count: > 0 })
            parameters["filterGoalIds"] = _filterGoalIds;

        await Shell.Current.GoToAsync("week-detail", parameters);
    }

    [RelayCommand]
    private async Task HibernateWeekAsync(TimelineWeekItem item)
    {
        var result = await _mediator.Send(new PauseWeekCommand(item.WeekId, WeekStatus.Hibernated));
        if (!result.IsSuccess)
        {
            var msg = result.Error == "week_already_started"
                ? "This week has already started and cannot be hibernated."
                : "Could not hibernate this week.";
            await Shell.Current.CurrentPage.DisplayAlertAsync("Hibernate Failed", msg, "OK");
            return;
        }
        item.Status = WeekStatus.Hibernated.ToString();
    }

    [RelayCommand]
    private async Task FreezeWeekAsync(TimelineWeekItem item)
    {
        var confirmed = await Shell.Current.CurrentPage.DisplayAlertAsync(
            "Emergency Freeze",
            "This will consume 1 Life Happens Shield. Continue?",
            "Freeze", "Cancel");
        if (!confirmed) return;

        var result = await _mediator.Send(new PauseWeekCommand(item.WeekId, WeekStatus.Frozen));
        if (!result.IsSuccess)
        {
            var msg = result.Error switch
            {
                "no_shields"           => "You have no Life Happens Shields available.",
                "freeze_window_closed" => "Emergency Freeze is only available before Friday of the target week.",
                _                      => "Could not freeze this week."
            };
            await Shell.Current.CurrentPage.DisplayAlertAsync("Freeze Failed", msg, "OK");
            return;
        }
        item.Status = WeekStatus.Frozen.ToString();
    }
}
