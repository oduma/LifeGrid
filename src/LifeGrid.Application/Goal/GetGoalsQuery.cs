using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Goal;

public record GetGoalsQuery : IRequest<Result<IReadOnlyList<GoalSummaryDto>>>;

public sealed class GetGoalsQueryHandler(
    IUserProfileRepository userProfileRepository,
    IGoalRepository        goalRepository,
    IWeekRepository        weekRepository)
    : IRequestHandler<GetGoalsQuery, Result<IReadOnlyList<GoalSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<GoalSummaryDto>>> Handle(
        GetGoalsQuery     request,
        CancellationToken cancellationToken)
    {
        var profile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (profile is null)
            return Result<IReadOnlyList<GoalSummaryDto>>.Success(Array.Empty<GoalSummaryDto>());

        var goals = await goalRepository.GetAllByUserIdAsync(profile.UserId, cancellationToken);

        var dtos = new List<GoalSummaryDto>(goals.Count);
        foreach (var g in goals)
        {
            var totalWeeks = await weekRepository.GetWeekGoalCountByGoalIdAsync(g.GoalId, cancellationToken);
            dtos.Add(new GoalSummaryDto(
                g.GoalId,
                g.Description,
                g.AmbientTag,
                g.Duration,
                g.DeadlineDate,
                g.Status.ToString(),
                totalWeeks));
        }

        return Result<IReadOnlyList<GoalSummaryDto>>.Success(dtos);
    }
}
