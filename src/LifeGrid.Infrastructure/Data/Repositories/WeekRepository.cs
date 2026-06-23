using LifeGrid.Application.Week;
using LifeGrid.Domain.Week;
using Microsoft.EntityFrameworkCore;
using WeekEntity     = LifeGrid.Domain.Week.Week;
using WeekGoalEntity = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Infrastructure.Data.Repositories;

internal sealed class WeekRepository(LifeGridDbContext db) : IWeekRepository
{
    public Task AddAsync(WeekEntity week, WeekGoalEntity weekGoal, CancellationToken ct = default)
    {
        db.Weeks.Add(week);
        db.WeekGoals.Add(weekGoal);
        return Task.CompletedTask;
    }

    public Task AddWeekGoalAsync(WeekGoalEntity weekGoal, CancellationToken ct = default)
    {
        db.WeekGoals.Add(weekGoal);
        return Task.CompletedTask;
    }

    public Task<WeekEntity?> GetActiveAsync(CancellationToken ct = default)
        => db.Weeks
             .Include(w => w.WeekGoals)
             .FirstOrDefaultAsync(w => w.Status == WeekStatus.Active, ct);

    public Task<WeekEntity?> GetByStartDateAsync(DateTime startDate, CancellationToken ct = default)
        => db.Weeks
             .Include(w => w.WeekGoals)
             .FirstOrDefaultAsync(w => w.StartDate.Date == startDate.Date, ct);

    public Task<int> GetWeekGoalCountByGoalIdAsync(Guid goalId, CancellationToken ct = default)
        => db.WeekGoals.CountAsync(wg => wg.GoalId == goalId, ct);

    public async Task<IReadOnlyList<WeekGoalEntity>> GetFutureWeekGoalsByGoalIdAsync(
        Guid goalId, DateTime afterDate, CancellationToken ct = default)
        => await db.WeekGoals
                   .Where(wg => wg.GoalId == goalId &&
                                db.Weeks.Any(w => w.WeekId == wg.WeekId && w.StartDate > afterDate))
                   .ToListAsync(ct);

    public Task RemoveWeekGoalRangeAsync(
        IReadOnlyList<WeekGoalEntity> weekGoals, CancellationToken ct = default)
    {
        db.WeekGoals.RemoveRange(weekGoals);
        return Task.CompletedTask;
    }

    public async Task<int> GetMaxWeekGoalNumberAsync(Guid goalId, CancellationToken ct = default)
    {
        var max = await db.WeekGoals
                          .Where(wg => wg.GoalId == goalId)
                          .Select(wg => (int?)wg.WeekGoalNumber)
                          .MaxAsync(ct);
        return max ?? 0;
    }

    public Task<int> GetHistoricalXpByGoalIdAsync(Guid goalId, CancellationToken ct = default)
        => db.WeekGoals
             .Where(wg => wg.GoalId == goalId)
             .SumAsync(wg => wg.GoalWeeklyXpEarned, ct);

    public Task<WeekEntity?> GetByIdAsync(Guid weekId, CancellationToken ct = default)
        => db.Weeks
             .Include(w => w.WeekGoals)
             .FirstOrDefaultAsync(w => w.WeekId == weekId, ct);

    public async Task<IReadOnlyList<WeekEntity>> GetTimelineAsync(CancellationToken ct = default)
        => await db.Weeks
                   .Include(w => w.WeekGoals)
                   .OrderBy(w => w.StartDate)
                   .ToListAsync(ct);
}
