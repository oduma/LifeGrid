using LifeGrid.Application.Goal;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using MediatR;
using GoalAggregate = LifeGrid.Domain.Goal.Goal;

namespace LifeGrid.Application.Timeline;

public record GetTimelineQuery : IRequest<Result<IReadOnlyList<TimelineWeekDto>>>;

public sealed class GetTimelineQueryHandler(
    IWeekRepository weekRepository,
    IGoalRepository goalRepository)
    : IRequestHandler<GetTimelineQuery, Result<IReadOnlyList<TimelineWeekDto>>>
{
    public async Task<Result<IReadOnlyList<TimelineWeekDto>>> Handle(
        GetTimelineQuery request, CancellationToken cancellationToken)
    {
        var weeks = await weekRepository.GetTimelineAsync(cancellationToken);

        var goalIds = weeks
            .SelectMany(w => w.WeekGoals.Select(wg => wg.GoalId))
            .Distinct()
            .ToList();

        IReadOnlyList<GoalAggregate> goals = goalIds.Count > 0
            ? await goalRepository.GetByIdsAsync(goalIds, cancellationToken)
            : Array.Empty<GoalAggregate>();

        var descMap = goals.ToDictionary(g => g.GoalId, g => g.Description);

        var dtos = weeks.Select(w => new TimelineWeekDto(
            w.WeekId,
            w.StartDate,
            w.Status.ToString(),
            w.TotalWeeklySpEarned,
            w.WeekGoals.Select(wg => new TimelineWeekGoalDto(
                descMap.GetValueOrDefault(wg.GoalId, string.Empty),
                wg.PenaltyState.ToString(),
                wg.GoalWeeklyGp,
                wg.GoalWeeklyXpEarned
            )).ToList()
        )).ToList();

        return Result<IReadOnlyList<TimelineWeekDto>>.Success(dtos);
    }
}
