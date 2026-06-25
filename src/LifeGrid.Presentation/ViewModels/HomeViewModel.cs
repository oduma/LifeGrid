using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LifeGrid.Application.Common;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.Home;
using LifeGrid.Application.MomentBurst;
using MediatR;
using System.Collections.ObjectModel;

namespace LifeGrid.Presentation.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly IMediator                  _mediator;
    private readonly IToastNotificationService  _toastService;
    private Guid _currentWeekId;

    public HomeViewModel(IMediator mediator, IToastNotificationService toastService)
    {
        _mediator     = mediator;
        _toastService = toastService;
        WeakReferenceMessenger.Default.Register<HomeViewModel, EconomyStateMutatedMessage>(this,
            async (r, _) => await MainThread.InvokeOnMainThreadAsync(r.LoadAsync));
    }

    [ObservableProperty] private string  _weekHeaderText            = string.Empty;
    [ObservableProperty] private string  _weekStatusText            = string.Empty;
    [ObservableProperty] private bool    _isEmptyStateVisible;
    [ObservableProperty] private bool    _isWeeklyDataVisible;
    [ObservableProperty] private string? _proofImageUrl;
    [ObservableProperty] private bool    _isProofImageOverlayVisible;
    [ObservableProperty] private bool    _isMomentBurstPending;

    public ObservableCollection<WeeklyGoalGroupItem> GoalGroups { get; } = new();

    public async Task LoadAsync()
    {
        var result  = await _mediator.Send(new GetCurrentWeekHabitsQuery());
        var hasData = result.IsSuccess && result.Value?.GoalGroups.Count > 0;

        IsWeeklyDataVisible = hasData;
        IsEmptyStateVisible = !hasData;

        if (!hasData)
        {
            GoalGroups.Clear();
            return;
        }

        var dto = result.Value!;
        _currentWeekId = dto.WeekId;
        WeekHeaderText = $"Current Week — {dto.StartDate:MMM dd, yyyy}";
        WeekStatusText = $"{dto.Status}  |  SP: {dto.TotalWeeklySpEarned}";

        GoalGroups.Clear();
        foreach (var g in dto.GoalGroups)
            GoalGroups.Add(new WeeklyGoalGroupItem(g, isFuture: false, isCurrentWeek: true));
    }

    [RelayCommand]
    private async Task OpenHabitLoggingAsync(WeeklyHabitItem item)
    {
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
    private async Task NavigateToCreateGoalAsync()
        => await Shell.Current.GoToAsync("create-goal");

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
