using WeekEntity     = LifeGrid.Domain.Week.Week;
using WeekGoalEntity = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.Week;

public interface IWeekRepository
{
    Task AddAsync(WeekEntity week, WeekGoalEntity weekGoal, CancellationToken ct = default);
    Task<WeekEntity?> GetActiveAsync(CancellationToken ct = default);
}
