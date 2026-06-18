using LifeGrid.Application.Goal;
using LifeGrid.Infrastructure.Data;
using GoalAggregate = LifeGrid.Domain.Goal.Goal;

namespace LifeGrid.Infrastructure.Data.Repositories;

internal sealed class GoalRepository(LifeGridDbContext db) : IGoalRepository
{
    public async Task AddAsync(GoalAggregate goal, CancellationToken ct = default)
    {
        db.Goals.Add(goal);
        await db.SaveChangesAsync(ct);
    }
}
