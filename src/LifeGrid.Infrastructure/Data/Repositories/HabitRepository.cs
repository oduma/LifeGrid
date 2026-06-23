using LifeGrid.Application.Gamification;
using LifeGrid.Application.Habit;
using Microsoft.EntityFrameworkCore;
using CompletedValueLog = LifeGrid.Domain.Habit.CompletedValueLog;
using HabitEntity = LifeGrid.Domain.Habit.Habit;

namespace LifeGrid.Infrastructure.Data.Repositories;

internal sealed class HabitRepository(LifeGridDbContext db) : IHabitRepository
{
    public Task AddRangeAsync(IReadOnlyList<HabitEntity> habits, CancellationToken ct = default)
    {
        db.Habits.AddRange(habits);
        return Task.CompletedTask;
    }

    public Task<HabitEntity?> GetByIdAsync(Guid habitId, CancellationToken ct = default)
        => db.Habits.FirstOrDefaultAsync(h => h.HabitId == habitId, ct);

    public Task AddCompletionLogAsync(CompletedValueLog log, CancellationToken ct = default)
    {
        db.CompletedValueLogs.Add(log);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<HabitEntity>> GetByWeekGoalIdAsync(
        Guid weekGoalId, CancellationToken ct = default)
        => await db.Habits
            .Where(h => h.WeekGoalId == weekGoalId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<HabitEntity>> GetByWeekGoalIdsAsync(
        IReadOnlyList<Guid> weekGoalIds, CancellationToken ct = default)
        => await db.Habits
            .Include(h => h.CompletedValuesLog)
            .Where(h => weekGoalIds.Contains(h.WeekGoalId))
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

    public async Task<IReadOnlyList<HabitCompletionSummaryDto>> GetCompletionSummariesForWeekGoalAsync(
        Guid weekGoalId, CancellationToken ct = default)
        => await db.Habits
            .Where(h => h.WeekGoalId == weekGoalId)
            .Select(h => new HabitCompletionSummaryDto(
                h.HabitId,
                h.TargetValue,
                db.CompletedValueLogs
                    .Where(l => l.HabitId == h.HabitId)
                    .Sum(l => (double?)l.ActualValue) ?? 0.0,
                h.HabitType))
            .ToListAsync(ct);
}
