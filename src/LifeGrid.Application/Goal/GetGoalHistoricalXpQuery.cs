using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Goal;

public record GetGoalHistoricalXpQuery(Guid GoalId) : IRequest<Result<int>>;

public sealed class GetGoalHistoricalXpQueryHandler(IWeekRepository weekRepository)
    : IRequestHandler<GetGoalHistoricalXpQuery, Result<int>>
{
    public async Task<Result<int>> Handle(
        GetGoalHistoricalXpQuery request, CancellationToken cancellationToken)
    {
        var xp = await weekRepository.GetHistoricalXpByGoalIdAsync(request.GoalId, cancellationToken);
        return Result<int>.Success(xp);
    }
}
