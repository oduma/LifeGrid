using LifeGrid.Application.Common;
using LifeGrid.Application.Habit;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Goal;

public record AbandonGoalCommand(Guid GoalId) : IRequest<Result>;

public sealed class AbandonGoalCommandHandler(
    IUserProfileRepository userProfileRepository,
    IGoalRepository        goalRepository,
    IWeekRepository        weekRepository,
    IHabitRepository       habitRepository,
    IUnitOfWork            unitOfWork)
    : IRequestHandler<AbandonGoalCommand, Result>
{
    public async Task<Result> Handle(AbandonGoalCommand request, CancellationToken cancellationToken)
    {
        var profile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (profile is null)
            return Result.Failure("User profile not found.");

        var goal = await goalRepository.GetByIdAsync(request.GoalId, cancellationToken);
        if (goal is null)
            return Result.Failure($"Goal {request.GoalId} not found.");

        var historicalXp = await weekRepository.GetHistoricalXpByGoalIdAsync(
            request.GoalId, cancellationToken);

        goal.MarkAbandoned();

        var futureWeekGoals = await weekRepository.GetFutureWeekGoalsByGoalIdAsync(
            request.GoalId, DateTime.UtcNow.Date, cancellationToken);

        var weekGoalIds = futureWeekGoals.Select(wg => wg.WeekGoalId).ToList();
        await habitRepository.RemoveByWeekGoalIdsAsync(weekGoalIds, cancellationToken);
        await weekRepository.RemoveWeekGoalRangeAsync(futureWeekGoals, cancellationToken);

        profile.DeductXp(historicalXp + 100);

        await unitOfWork.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
