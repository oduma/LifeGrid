using CommunityToolkit.Mvvm.ComponentModel;
using LifeGrid.Application.WeeklyHabits;

namespace LifeGrid.Presentation.ViewModels;

public sealed partial class WeeklyHabitItem : ObservableObject
{
    private readonly DateTime _deadlineDateTime;

    public WeeklyHabitItem(
        WeeklyHabitItemDto dto,
        string             goalDescription,
        string             weekLabel,
        bool               isInteractive)
    {
        HabitId          = dto.HabitId;
        HabitTypeLabel   = dto.HabitType;
        HabitName        = dto.HabitName;
        HabitDescription = dto.HabitDescription;
        TargetText       = $"{dto.TargetValue} {dto.MeasurementUnit} by {dto.DeadlineDateTime:MMM dd}";
        MeasurementUnit  = dto.MeasurementUnit;
        GoalDescription  = goalDescription;
        WeekLabel        = weekLabel;
        IsInteractive    = isInteractive;
        _deadlineDateTime = dto.DeadlineDateTime;
        CompletionLogs   = dto.CompletionLogs
            .Select(l => new HabitCompletionLogItem(
                l.LogId, l.ActualValue, l.MeasurementUnit,
                l.ProofText, l.ProofImageUrl, l.Timestamp))
            .ToList();

        RefreshCountdown(DateTime.UtcNow);
    }

    public Guid     HabitId            { get; }
    public string   HabitTypeLabel     { get; }
    public string   HabitName          { get; }
    public string   HabitDescription   { get; }
    public string   TargetText         { get; }
    public string   MeasurementUnit    { get; }
    public string   GoalDescription    { get; }
    public string   WeekLabel          { get; }
    public bool     IsInteractive      { get; }
    public bool     IsMomentBurst      => HabitTypeLabel == "MomentBurst";
    public bool     IsNotMomentBurst   => !IsMomentBurst;
    public bool     IsFlash            => HabitTypeLabel == "Flash";
    public IReadOnlyList<HabitCompletionLogItem> CompletionLogs { get; }

    [ObservableProperty] private string _countdownText = string.Empty;

    public void RefreshCountdown(DateTime nowUtc)
    {
        var remaining = _deadlineDateTime - nowUtc;
        CountdownText = remaining <= TimeSpan.Zero
            ? "Expired"
            : $"Expires in {(int)remaining.TotalHours}h {remaining.Minutes}m";
    }
}
