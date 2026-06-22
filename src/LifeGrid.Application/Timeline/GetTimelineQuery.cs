using LifeGrid.Application.Goal;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using MediatR;
using GoalAggregate = LifeGrid.Domain.Goal.Goal;

namespace LifeGrid.Application.Timeline;

public record GetTimelineQuery(IReadOnlyList<Guid>? FilterGoalIds = null)
    : IRequest<Result<IReadOnlyList<TimelineWeekDto>>>;

public sealed class GetTimelineQueryHandler(
    IWeekRepository weekRepository,
    IGoalRepository goalRepository)
    : IRequestHandler<GetTimelineQuery, Result<IReadOnlyList<TimelineWeekDto>>>
{
    public async Task<Result<IReadOnlyList<TimelineWeekDto>>> Handle(
        GetTimelineQuery request, CancellationToken cancellationToken)
    {
        var weeks = await weekRepository.GetTimelineAsync(cancellationToken);

        HashSet<Guid>? filterSet = request.FilterGoalIds is { Count: > 0 } f
            ? f.ToHashSet()
            : null;

        // Build (Week, filtered WeekGoals) pairs — drop weeks with zero matching items.
        var filtered = weeks
            .Select(w => (Week: w, Goals: filterSet is null
                ? w.WeekGoals.ToList()
                : w.WeekGoals.Where(wg => filterSet.Contains(wg.GoalId)).ToList()))
            .Where(t => filterSet is null || t.Goals.Count > 0)
            .ToList();

        var neededGoalIds = filtered
            .SelectMany(t => t.Goals.Select(wg => wg.GoalId))
            .Distinct()
            .ToList();

        IReadOnlyList<GoalAggregate> goals = neededGoalIds.Count > 0
            ? await goalRepository.GetByIdsAsync(neededGoalIds, cancellationToken)
            : Array.Empty<GoalAggregate>();

        var descMap = goals.ToDictionary(g => g.GoalId, g => g.Description);

        var dtos = filtered.Select(t => new TimelineWeekDto(
            t.Week.WeekId,
            t.Week.StartDate,
            t.Week.Status.ToString(),
            t.Week.TotalWeeklySpEarned,
            t.Goals.Select(wg => new TimelineWeekGoalDto(
                descMap.GetValueOrDefault(wg.GoalId, string.Empty),
                wg.PenaltyState.ToString(),
                wg.GoalWeeklyGp,
                wg.GoalWeeklyXpEarned
            )).ToList()
        )).ToList();

        return Result<IReadOnlyList<TimelineWeekDto>>.Success(dtos);
    }
}
