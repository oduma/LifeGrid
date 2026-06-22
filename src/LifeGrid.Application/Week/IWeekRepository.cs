using WeekEntity     = LifeGrid.Domain.Week.Week;
using WeekGoalEntity = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.Week;

public interface IWeekRepository
{
    Task AddAsync(WeekEntity week, WeekGoalEntity weekGoal, CancellationToken ct = default);
    Task AddWeekGoalAsync(WeekGoalEntity weekGoal, CancellationToken ct = default);
    Task<WeekEntity?> GetActiveAsync(CancellationToken ct = default);
    Task<WeekEntity?> GetByStartDateAsync(DateTime startDate, CancellationToken ct = default);
    Task<int> GetWeekGoalCountByGoalIdAsync(Guid goalId, CancellationToken ct = default);
    Task<IReadOnlyList<WeekGoalEntity>> GetFutureWeekGoalsByGoalIdAsync(Guid goalId, DateTime afterDate, CancellationToken ct = default);
    Task RemoveWeekGoalRangeAsync(IReadOnlyList<WeekGoalEntity> weekGoals, CancellationToken ct = default);
    Task<int> GetMaxWeekGoalNumberAsync(Guid goalId, CancellationToken ct = default);
    Task<int> GetHistoricalXpByGoalIdAsync(Guid goalId, CancellationToken ct = default);
    Task<IReadOnlyList<WeekEntity>> GetTimelineAsync(CancellationToken ct = default);
}
