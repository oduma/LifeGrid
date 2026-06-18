using LifeGrid.Application.Goal;
using Microsoft.EntityFrameworkCore;
using GoalAggregate = LifeGrid.Domain.Goal.Goal;

namespace LifeGrid.Infrastructure.Data.Repositories;

internal sealed class GoalRepository(LifeGridDbContext db) : IGoalRepository
{
    public async Task AddAsync(GoalAggregate goal, CancellationToken ct = default)
    {
        db.Goals.Add(goal);
        await db.SaveChangesAsync(ct);
    }

    public Task<GoalAggregate?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => db.Goals
             .FirstOrDefaultAsync(g => g.UserId == userId, ct);

    public async Task<IReadOnlyList<GoalAggregate>> GetAllByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await db.Goals
                   .Where(g => g.UserId == userId)
                   .ToListAsync(ct);
}
