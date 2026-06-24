using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Onboarding.Commands;
using LifeGrid.Application.Timeline;
using LifeGrid.Application.Vice;
using MediatR;
using System.Collections.ObjectModel;

namespace LifeGrid.Presentation.ViewModels;

public partial class GoalsViewModel(IMediator mediator) : ObservableObject
{
    public ObservableCollection<GoalSummaryItem> Goals { get; } = new();

    [ObservableProperty] private bool _isViceSurveyBannerVisible;

    // ── Data loading ──────────────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        var result = await mediator.Send(new GetGoalsQuery());
        if (!result.IsSuccess) return;

        Goals.Clear();
        foreach (var dto in result.Value!)
            Goals.Add(new GoalSummaryItem
            {
                GoalId       = dto.GoalId,
                Description  = dto.Description,
                AmbientTag   = dto.AmbientTag,
                Duration     = dto.Duration,
                DeadlineDate = dto.DeadlineDate,
                Status       = dto.Status,
                TotalWeeks   = dto.TotalWeeks,
            });

        var availResult = await mediator.Send(new GetViceSurveyAvailabilityQuery());
        IsViceSurveyBannerVisible = availResult.IsSuccess && availResult.Value;
    }

    // ── Selection state ───────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMultiSelectMode))]
    [NotifyPropertyChangedFor(nameof(IsAddGoalVisible))]
    private GoalSelectionMode _selectionMode = GoalSelectionMode.Standard;

    public bool IsMultiSelectMode             => SelectionMode == GoalSelectionMode.MultiSelect;
    public bool IsAddGoalVisible              => !IsMultiSelectMode;
    public bool IsViewFilteredTimelineVisible => IsMultiSelectMode && Goals.Any(g => g.IsSelected);

    [RelayCommand]
    private void EnterMultiSelect(GoalSummaryItem item)
    {
        SelectionMode   = GoalSelectionMode.MultiSelect;
        item.IsSelected = true;
        OnPropertyChanged(nameof(IsViewFilteredTimelineVisible));
    }

    [RelayCommand]
    private void ToggleGoalSelection(GoalSummaryItem item)
    {
        item.IsSelected = !item.IsSelected;
        OnPropertyChanged(nameof(IsViewFilteredTimelineVisible));
    }

    [RelayCommand]
    private async Task NavigateToGoalTimelineAsync(GoalSummaryItem item)
    {
        if (IsMultiSelectMode)
        {
            ToggleGoalSelection(item);
            return;
        }
        await Shell.Current.GoToAsync("//timeline",
            new ShellNavigationQueryParameters
            {
                ["filterGoalIds"] = (IReadOnlyList<Guid>)new[] { item.GoalId }
            });
    }

    [RelayCommand]
    private async Task ViewFilteredTimelineAsync()
    {
        var ids = Goals.Where(g => g.IsSelected).Select(g => g.GoalId).ToList();
        ResetSelectionState();
        await Shell.Current.GoToAsync("//timeline",
            new ShellNavigationQueryParameters
            {
                ["filterGoalIds"] = (IReadOnlyList<Guid>)ids
            });
    }

    [RelayCommand]
    private void ExitMultiSelect() => ResetSelectionState();

    public void ResetSelectionState()
    {
        foreach (var g in Goals) g.IsSelected = false;
        SelectionMode = GoalSelectionMode.Standard;
        OnPropertyChanged(nameof(IsViewFilteredTimelineVisible));
    }

    // ── Swipe commands ────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LaunchViceSurveyAsync()
    {
        var result = await mediator.Send(new LaunchViceSurveyCommand());
        if (!result.IsSuccess) return;
        await Shell.Current.GoToAsync("vice-survey");
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
