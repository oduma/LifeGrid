using LifeGrid.Application.Week;
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
}
