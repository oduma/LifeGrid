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
        => db.Weeks.FirstOrDefaultAsync(w => w.StartDate.Date == startDate.Date, ct);
}
