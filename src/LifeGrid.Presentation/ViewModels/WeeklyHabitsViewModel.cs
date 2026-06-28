using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LifeGrid.Application.Common;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.MomentBurst;
using LifeGrid.Application.Week;
using LifeGrid.Application.WeeklyHabits;
using MediatR;
using System.Collections.ObjectModel;

namespace LifeGrid.Presentation.ViewModels;

public partial class WeeklyHabitsViewModel : ObservableObject, IQueryAttributable
{
    private readonly IMediator                 _mediator;
    private readonly IToastNotificationService _toastService;

    private Guid                 _weekId;
    private IReadOnlyList<Guid>? _filterGoalIds;

    public WeeklyHabitsViewModel(IMediator mediator, IToastNotificationService toastService)
    {
        _mediator     = mediator;
        _toastService = toastService;
        WeakReferenceMessenger.Default.Register<WeeklyHabitsViewModel, EconomyStateMutatedMessage>(this,
            async (r, _) => await MainThread.InvokeOnMainThreadAsync(r.LoadAsync));
    }

    [ObservableProperty] private string  _weekHeaderText            = string.Empty;
    [ObservableProperty] private string  _weekStatusText            = string.Empty;
    [ObservableProperty] private string? _proofImageUrl;
    [ObservableProperty] private bool    _isProofImageOverlayVisible;
    [ObservableProperty] private bool    _isMomentBurstPending;
    [ObservableProperty] private bool    _isCloseWeekButtonVisible;
    [ObservableProperty] private bool    _isSummaryButtonVisible;
    [ObservableProperty] private bool    _isLoggingEnabled         = true;

    public ObservableCollection<WeeklyGoalGroupItem> GoalGroups { get; } = new();

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("weekId", out var wid))
        {
            if (wid is Guid g)                            _weekId = g;
            else if (wid is string s && Guid.TryParse(s, out var parsed)) _weekId = parsed;
        }

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

        var today         = DateTime.Today;
        int daysFromMon   = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var currentMonday = today.AddDays(-daysFromMon);
        var isFuture      = dto.StartDate.Date > today;
        // Use DateOnly to avoid Kind.Unspecified vs. Kind.Local mismatch from SQLite.
        var isCurrentWeek = DateOnly.FromDateTime(dto.StartDate) == DateOnly.FromDateTime(currentMonday);

        var (isLogging, isClose, isSummary) =
            WeekClosureStateComputer.Compute(dto.Status, dto.StartDate, DateTime.UtcNow);
        IsLoggingEnabled         = isLogging;
        IsCloseWeekButtonVisible = isClose;
        IsSummaryButtonVisible   = isSummary;

        GoalGroups.Clear();
        foreach (var g in dto.GoalGroups)
            GoalGroups.Add(new WeeklyGoalGroupItem(g, isFuture, isCurrentWeek,
                                                   isLoggingEnabled: isLogging));
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
    private async Task IWantMoreAsync(WeeklyGoalGroupItem item)
    {
        var userInput = await Shell.Current.CurrentPage.DisplayPromptAsync(
            "I Want More", "What are you looking for?");

        if (string.IsNullOrWhiteSpace(userInput)) return;

        IsMomentBurstPending = true;
        try
        {
            var result = await _mediator.Send(
                new RequestMomentBurstCommand(item.WeekGoalId, userInput));

            if (!result.IsSuccess) return;

            if (result.Value is MomentBurstOutcome.Denied denied)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    Shell.Current.CurrentPage!.DisplayAlertAsync("Keep the Focus", denied.Message, "OK"));
                return;
            }

            var created      = (MomentBurstOutcome.HabitCreated)result.Value!;
            var isOnThisPage = Shell.Current.CurrentPage?.BindingContext == this;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (isOnThisPage)
                    await LoadAsync();
                else
                    await _toastService.ShowInfoAsync(
                        "Moment Burst Added!",
                        $"'{created.NewHabit.HabitName}' has been added to your goal.");
            });
        }
        finally
        {
            IsMomentBurstPending = false;
        }
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

    [RelayCommand]
    private async Task CloseWeekAsync()
    {
        var result = await _mediator.Send(new CloseWeekCommand(_weekId));
        if (result.IsSuccess)
            await LoadAsync();
    }

    [RelayCommand]
    private Task GoToSummaryAsync()
        => Shell.Current.GoToAsync($"week-summary?weekId={_weekId}");
}
