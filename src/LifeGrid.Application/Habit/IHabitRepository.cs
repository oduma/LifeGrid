using LifeGrid.Application.Gamification;
using CompletedValueLog = LifeGrid.Domain.Habit.CompletedValueLog;
using HabitEntity = LifeGrid.Domain.Habit.Habit;

namespace LifeGrid.Application.Habit;

public interface IHabitRepository
{
    Task AddRangeAsync(IReadOnlyList<HabitEntity> habits, CancellationToken ct = default);
    Task<HabitEntity?> GetByIdAsync(Guid habitId, CancellationToken ct = default);
    Task<IReadOnlyList<HabitEntity>> GetByWeekGoalIdAsync(Guid weekGoalId, CancellationToken ct = default);
    Task<IReadOnlyList<HabitEntity>> GetByWeekGoalIdsAsync(IReadOnlyList<Guid> weekGoalIds, CancellationToken ct = default);
    Task RemoveByWeekGoalIdsAsync(IReadOnlyList<Guid> weekGoalIds, CancellationToken ct = default);
    Task AddCompletionLogAsync(CompletedValueLog log, CancellationToken ct = default);
    Task<IReadOnlyList<HabitCompletionSummaryDto>> GetCompletionSummariesForWeekGoalAsync(
        Guid weekGoalId, CancellationToken ct = default);
}
