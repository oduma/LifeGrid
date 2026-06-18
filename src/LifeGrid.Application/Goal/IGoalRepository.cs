using GoalAggregate = LifeGrid.Domain.Goal.Goal;

namespace LifeGrid.Application.Goal;

public interface IGoalRepository
{
    Task AddAsync(GoalAggregate goal, CancellationToken ct = default);
    Task<GoalAggregate?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<GoalAggregate>> GetAllByUserIdAsync(Guid userId, CancellationToken ct = default);
}
