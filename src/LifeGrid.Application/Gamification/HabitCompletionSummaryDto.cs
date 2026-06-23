using LifeGrid.Domain.Habit;

namespace LifeGrid.Application.Gamification;

public record HabitCompletionSummaryDto(
    Guid      HabitId,
    double    TargetValue,
    double    TotalActualValue,
    HabitType HabitType);
