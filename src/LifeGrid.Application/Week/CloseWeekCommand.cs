using LifeGrid.Application.Common;
using LifeGrid.Application.Gamification;
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Week;

public record CloseWeekCommand(Guid WeekId) : IRequest<Result>;

public sealed class CloseWeekCommandHandler(
    IWeekRepository          weekRepository,
    IUnitOfWork              unitOfWork,
    IEconomyStateBroadcaster broadcaster)
    : IRequestHandler<CloseWeekCommand, Result>
{
    public async Task<Result> Handle(CloseWeekCommand request, CancellationToken cancellationToken)
    {
        var week = await weekRepository.GetByIdAsync(request.WeekId, cancellationToken);
        if (week is null)
            return Result.Failure("week_not_found");

        week.Close();
        await unitOfWork.CommitAsync(cancellationToken);
        broadcaster.Broadcast();
        return Result.Success();
    }
}
