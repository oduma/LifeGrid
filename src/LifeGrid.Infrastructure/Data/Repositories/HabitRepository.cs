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
}
