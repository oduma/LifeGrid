using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeGrid.Application.Common;
using LifeGrid.Application.ViceCheck;
using LifeGrid.Application.Week;
using LifeGrid.Application.WeeklyHabits;
using MediatR;
using System.Collections.ObjectModel;

namespace LifeGrid.Presentation.ViewModels;

public partial class WeekSummaryViewModel : ObservableObject, IQueryAttributable
{
    private readonly IMediator                 _mediator;
    private readonly IToastNotificationService _toastService;
    private Guid  _weekId;
    private Guid? _currentAuditId;

    public WeekSummaryViewModel(IMediator mediator, IToastNotificationService toastService)
    {
        _mediator     = mediator;
        _toastService = toastService;
    }

    [ObservableProperty] private string _weekHeaderText      = string.Empty;
    [ObservableProperty] private string _weekStatusText      = string.Empty;
    [ObservableProperty] private bool   _hasShieldsAvailable;
    [ObservableProperty] private bool   _isViceCheckAvailable;
    [ObservableProperty] private bool   _isViceCheckOverlayVisible;
    [ObservableProperty] private string _viceCheckQuestion = string.Empty;
    [ObservableProperty] private string _viceCheckAnswer   = string.Empty;
    [ObservableProperty] private bool   _isViceCheckBusy;

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
        WeekHeaderText       = dto.StartDate.ToString("MMM dd, yyyy");
        WeekStatusText       = $"{dto.Status}  |  SP: {dto.TotalWeeklySpEarned}";
        HasShieldsAvailable  = dto.ShieldsAvailable > 0;
        IsViceCheckAvailable = dto.IsViceCheckAvailable;

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

    [RelayCommand]
    private async Task InitiateViceCheckAsync()
    {
        var result = await _mediator.Send(new InitiateViceCheckCommand(_weekId));
        if (!result.IsSuccess)
        {
            await _toastService.ShowErrorAsync("Unavailable", result.Error ?? "Could not start the check.");
            return;
        }

        _currentAuditId           = result.Value!.AuditId;
        ViceCheckQuestion         = result.Value.Question;
        ViceCheckAnswer           = string.Empty;
        IsViceCheckOverlayVisible = true;
    }

    [RelayCommand]
    private async Task SubmitViceCheckAnswerAsync()
    {
        if (string.IsNullOrWhiteSpace(ViceCheckAnswer) || _currentAuditId is null) return;

        IsViceCheckBusy = true;
        var result = await _mediator.Send(new ResolveViceCheckCommand(_currentAuditId.Value, ViceCheckAnswer));
        IsViceCheckBusy           = false;
        IsViceCheckOverlayVisible = false;

        if (!result.IsSuccess)
        {
            await _toastService.ShowErrorAsync("Error", result.Error ?? "Could not resolve the check.");
            return;
        }

        if (result.Value!.Persists)
            await _toastService.ShowErrorAsync("Vice Detected",
                $"Vice detected. -{result.Value.PenaltyPercent:F0}% GP applied retroactively.");
        else
            await _toastService.ShowInfoAsync("Integrity Maintained", "Integrity maintained. 20 XP secured.");

        await LoadAsync();
    }
}
