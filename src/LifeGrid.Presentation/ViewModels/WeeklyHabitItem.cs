using LifeGrid.Application.WeeklyHabits;

namespace LifeGrid.Presentation.ViewModels;

public sealed class WeeklyHabitItem
{
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
        CompletionLogs   = dto.CompletionLogs
            .Select(l => new HabitCompletionLogItem(
                l.LogId, l.ActualValue, l.MeasurementUnit,
                l.ProofText, l.ProofImageUrl, l.Timestamp))
            .ToList();
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
    public IReadOnlyList<HabitCompletionLogItem> CompletionLogs { get; }
}
