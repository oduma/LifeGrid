using LifeGrid.Application.Common;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.Goal;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.Gamification;
using LifeGrid.Domain.WeekGoal;
using MediatR;

namespace LifeGrid.Application.Week;

public record CloseWeekCommand(Guid WeekId) : IRequest<Result<CloseWeekCommandResult>>;

public record CloseWeekCommandResult(Guid? OverwhelmedGoalId);

public sealed class CloseWeekCommandHandler(
    IWeekRepository          weekRepository,
    IGoalRepository          goalRepository,
    IUnitOfWork              unitOfWork,
    IEconomyStateBroadcaster broadcaster)
    : IRequestHandler<CloseWeekCommand, Result<CloseWeekCommandResult>>
{
    public async Task<Result<CloseWeekCommandResult>> Handle(
        CloseWeekCommand request, CancellationToken cancellationToken)
    {
        var week = await weekRepository.GetByIdAsync(request.WeekId, cancellationToken);
        if (week is null)
            return Result<CloseWeekCommandResult>.Failure("week_not_found");

        Guid? overwhelmedGoalId = null;

        foreach (var weekGoal in week.WeekGoals)
        {
            var prev      = await weekRepository.GetPreviousWeekGoalAsync(weekGoal.GoalId, week.WeekNumber, cancellationToken);
            var prevState = prev?.PenaltyState ?? PenaltyState.Clean;
            var esc       = ProcrastinationEscalationEngine.Evaluate(prevState, weekGoal.GoalWeeklyGp, weekGoal.GoalWeeklyXpEarned);

            weekGoal.SetPenaltyState(esc.NewPenaltyState);
            weekGoal.ApplyXpPenalty(esc.PenalizedXp);

            if (esc.TriggersOverwhelmed)
            {
                var goal = await goalRepository.GetByIdAsync(weekGoal.GoalId, cancellationToken);
                goal?.MarkOverwhelmed();
                overwhelmedGoalId = weekGoal.GoalId;
            }
        }

        week.Close();
        await unitOfWork.CommitAsync(cancellationToken);
        broadcaster.Broadcast();

        return Result<CloseWeekCommandResult>.Success(new(overwhelmedGoalId));
    }
}
