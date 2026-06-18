using GoalAggregate = LifeGrid.Domain.Goal.Goal;

namespace LifeGrid.Application.Goal;

public interface IGoalRepository
{
    Task AddAsync(GoalAggregate goal, CancellationToken ct = default);
}
