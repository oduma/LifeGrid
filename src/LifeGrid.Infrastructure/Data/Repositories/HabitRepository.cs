using LifeGrid.Application.Habit;
using Microsoft.EntityFrameworkCore;
using HabitEntity = LifeGrid.Domain.Habit.Habit;

namespace LifeGrid.Infrastructure.Data.Repositories;

internal sealed class HabitRepository(LifeGridDbContext db) : IHabitRepository
{
    public Task AddRangeAsync(IReadOnlyList<HabitEntity> habits, CancellationToken ct = default)
    {
        db.Habits.AddRange(habits);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<HabitEntity>> GetByWeekGoalIdAsync(
        Guid weekGoalId, CancellationToken ct = default)
        => await db.Habits
            .Where(h => h.WeekGoalId == weekGoalId)
            .ToListAsync(ct);

    public async Task RemoveByWeekGoalIdsAsync(
        IReadOnlyList<Guid> weekGoalIds, CancellationToken ct = default)
    {
        if (weekGoalIds.Count == 0) return;
        var habits = await db.Habits
            .Where(h => weekGoalIds.Contains(h.WeekGoalId))
            .ToListAsync(ct);
        db.Habits.RemoveRange(habits);
    }
}
