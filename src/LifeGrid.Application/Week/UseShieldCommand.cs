using LifeGrid.Application.Common;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.WeekGoal;
using MediatR;

namespace LifeGrid.Application.Week;

public record UseShieldCommand(Guid WeekGoalId) : IRequest<Result>;

public sealed class UseShieldCommandHandler(
    IWeekRepository          weekRepository,
    IUserProfileRepository   profileRepository,
    IUnitOfWork              unitOfWork,
    IEconomyStateBroadcaster broadcaster)
    : IRequestHandler<UseShieldCommand, Result>
{
    public async Task<Result> Handle(UseShieldCommand request, CancellationToken cancellationToken)
    {
        var weekGoal = await weekRepository.GetWeekGoalByIdAsync(request.WeekGoalId, cancellationToken);
        if (weekGoal is null)
            return Result.Failure("weekgoal_not_found");

        if (weekGoal.PenaltyState != PenaltyState.Level1Warning)
            return Result.Failure("invalid_state");

        var profile = await profileRepository.GetSingleAsync(cancellationToken);
        if (profile is null)
            return Result.Failure("profile_not_found");

        if (!profile.ConsumeShield())
            return Result.Failure("no_shields");

        weekGoal.SetPenaltyState(PenaltyState.Clean);
        await unitOfWork.CommitAsync(cancellationToken);
        broadcaster.Broadcast();

        return Result.Success();
    }
}
