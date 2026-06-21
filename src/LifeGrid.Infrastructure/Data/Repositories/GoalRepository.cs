using LifeGrid.Application.Goal;
using LifeGrid.Domain.Goal;
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

    public Task<int> GetActiveCountAsync(Guid userId, CancellationToken ct = default)
        => db.Goals
             .CountAsync(g => g.UserId == userId && g.Status == GoalStatus.Active, ct);

    public Task<GoalAggregate?> GetByIdAsync(Guid goalId, CancellationToken ct = default)
        => db.Goals
             .Include(g => g.RefinementAnswers)
             .FirstOrDefaultAsync(g => g.GoalId == goalId, ct);
}
