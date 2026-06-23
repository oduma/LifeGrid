using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.WeeklyHabits;
using MediatR;
using System.Collections.ObjectModel;

namespace LifeGrid.Presentation.ViewModels;

public partial class WeeklyHabitsViewModel : ObservableObject, IQueryAttributable
{
    private readonly IMediator _mediator;

    private Guid                 _weekId;
    private IReadOnlyList<Guid>? _filterGoalIds;

    public WeeklyHabitsViewModel(IMediator mediator)
    {
        _mediator = mediator;
        WeakReferenceMessenger.Default.Register<WeeklyHabitsViewModel, EconomyStateMutatedMessage>(this,
            async (r, _) => await r.LoadAsync());
    }

    [ObservableProperty] private string  _weekHeaderText           = string.Empty;
    [ObservableProperty] private string  _weekStatusText           = string.Empty;
    [ObservableProperty] private string? _proofImageUrl;
    [ObservableProperty] private bool    _isProofImageOverlayVisible;

    public ObservableCollection<WeeklyGoalGroupItem> GoalGroups { get; } = new();

    // For pushed pages ApplyQueryAttributes fires BEFORE OnAppearing — LoadAsync is called
    // from OnAppearing after _weekId and _filterGoalIds are already set.
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("weekId", out var wid) && wid is Guid weekId)
            _weekId = weekId;

        _filterGoalIds = query.TryGetValue("filterGoalIds", out var fids) &&
                         fids is IReadOnlyList<Guid> { Count: > 0 } ids
            ? ids : null;
    }

    public async Task LoadAsync()
    {
        var result = await _mediator.Send(new GetWeeklyHabitsQuery(_weekId, _filterGoalIds));
        if (!result.IsSuccess) return;

        var dto        = result.Value!;
        WeekHeaderText = dto.StartDate.ToString("MMM dd, yyyy");
        WeekStatusText = $"{dto.Status}  |  SP: {dto.TotalWeeklySpEarned}";

        var isFuture = dto.StartDate.Date > DateTime.Today;

        GoalGroups.Clear();
        foreach (var g in dto.GoalGroups)
            GoalGroups.Add(new WeeklyGoalGroupItem(g, isFuture));
    }

    [RelayCommand]
    private async Task OpenHabitLoggingAsync(WeeklyHabitItem item)
    {
        if (!item.IsInteractive) return;
        await Shell.Current.GoToAsync("habit-logging", new ShellNavigationQueryParameters
        {
            ["habitId"]         = item.HabitId,
            ["habitName"]       = item.HabitName,
            ["habitDescription"]= item.HabitDescription,
            ["targetText"]      = item.TargetText,
            ["measurementUnit"] = item.MeasurementUnit,
            ["goalDescription"] = item.GoalDescription,
            ["weekLabel"]       = item.WeekLabel
        });
    }

    [RelayCommand]
    private void ShowProofImage(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        ProofImageUrl              = url;
        IsProofImageOverlayVisible = true;
    }

    [RelayCommand]
    private void DismissProofImage()
    {
        IsProofImageOverlayVisible = false;
        ProofImageUrl              = null;
    }
}
