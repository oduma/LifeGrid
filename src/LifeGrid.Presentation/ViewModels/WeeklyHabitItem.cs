using LifeGrid.Application.WeeklyHabits;

namespace LifeGrid.Presentation.ViewModels;

public sealed class WeeklyHabitItem
{
    public WeeklyHabitItem(WeeklyHabitItemDto dto)
    {
        HabitId          = dto.HabitId;
        HabitTypeLabel   = dto.HabitType;
        HabitName        = dto.HabitName;
        HabitDescription = dto.HabitDescription;
        TargetText       = $"{dto.TargetValue} {dto.MeasurementUnit} by {dto.DeadlineDateTime:MMM dd}";
        CompletionLogs   = dto.CompletionLogs
            .Select(l => new HabitCompletionLogItem(
                l.LogId, l.ActualValue, l.MeasurementUnit,
                l.ProofText, l.ProofImageUrl, l.Timestamp))
            .ToList();
    }

    public Guid     HabitId          { get; }
    public string   HabitTypeLabel   { get; }
    public string   HabitName        { get; }
    public string   HabitDescription { get; }
    public string   TargetText       { get; }
    public IReadOnlyList<HabitCompletionLogItem> CompletionLogs { get; }
}
