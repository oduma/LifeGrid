using HabitEntity = LifeGrid.Domain.Habit.Habit;

namespace LifeGrid.Application.Habit;

public interface IHabitRepository
{
    Task AddRangeAsync(IReadOnlyList<HabitEntity> habits, CancellationToken ct = default);
    Task<IReadOnlyList<HabitEntity>> GetByWeekGoalIdAsync(Guid weekGoalId, CancellationToken ct = default);
    Task RemoveByWeekGoalIdsAsync(IReadOnlyList<Guid> weekGoalIds, CancellationToken ct = default);
}
