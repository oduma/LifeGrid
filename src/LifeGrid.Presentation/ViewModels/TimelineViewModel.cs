using CommunityToolkit.Mvvm.ComponentModel;
using LifeGrid.Application.Timeline;
using MediatR;
using System.Collections.ObjectModel;

namespace LifeGrid.Presentation.ViewModels;

public partial class TimelineViewModel(IMediator mediator) : ObservableObject
{
    private int _activeWeekIndex = -1;

    public ObservableCollection<TimelineWeekItem> Weeks { get; } = new();

    public int ActiveWeekIndex => _activeWeekIndex;

    public async Task LoadAsync()
    {
        var result = await mediator.Send(new GetTimelineQuery());
        if (!result.IsSuccess) return;

        Weeks.Clear();
        _activeWeekIndex = -1;

        foreach (var dto in result.Value!)
        {
            Weeks.Add(new TimelineWeekItem
            {
                WeekId              = dto.WeekId,
                StartDate           = dto.StartDate,
                Status              = dto.Status,
                TotalWeeklySpEarned = dto.TotalWeeklySpEarned,
                Goals               = dto.Goals.Select(g => new TimelineWeekGoalItem(
                    g.GoalDescription,
                    g.PenaltyState,
                    g.GoalWeeklyGp,
                    g.GoalWeeklyXpEarned)).ToList()
            });
        }

        // Mark the first Active-status week as the initial focus; fallback to the last week
        _activeWeekIndex = FindInitialActiveIndex();
        if (_activeWeekIndex >= 0)
            Weeks[_activeWeekIndex].IsActive = true;
    }

    public void SetActiveWeekByIndex(int index)
    {
        if (index < 0 || index >= Weeks.Count) return;
        if (index == _activeWeekIndex) return;

        if (_activeWeekIndex >= 0 && _activeWeekIndex < Weeks.Count)
            Weeks[_activeWeekIndex].IsActive = false;

        _activeWeekIndex = index;
        Weeks[index].IsActive = true;
    }

    private int FindInitialActiveIndex()
    {
        if (Weeks.Count == 0) return -1;
        for (int i = 0; i < Weeks.Count; i++)
            if (Weeks[i].Status == "Active") return i;
        return Weeks.Count - 1;
    }
}
