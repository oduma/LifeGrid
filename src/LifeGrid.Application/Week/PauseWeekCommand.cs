using LifeGrid.Application.Common;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.Week;
using MediatR;

namespace LifeGrid.Application.Week;

public record PauseWeekCommand(Guid WeekId, WeekStatus PauseType) : IRequest<Result>;

public sealed class PauseWeekCommandHandler(
    IWeekRepository          weekRepository,
    IUserProfileRepository   userProfileRepository,
    IDateTimeProvider        dateTimeProvider,
    IUnitOfWork              unitOfWork,
    IEconomyStateBroadcaster broadcaster)
    : IRequestHandler<PauseWeekCommand, Result>
{
    public async Task<Result> Handle(PauseWeekCommand request, CancellationToken cancellationToken)
    {
        var week = await weekRepository.GetByIdAsync(request.WeekId, cancellationToken);
        if (week is null)
            return Result.Failure("week_not_found");

        var today = dateTimeProvider.UtcNow.Date;

        if (request.PauseType == WeekStatus.Hibernated)
            return await HibernateAsync(week, today, cancellationToken);

        if (request.PauseType == WeekStatus.Frozen)
            return await FreezeAsync(week, today, cancellationToken);

        return Result.Failure("invalid_pause_type");
    }

    private async Task<Result> HibernateAsync(
        Domain.Week.Week week, DateTime today, CancellationToken ct)
    {
        if (week.StartDate.Date <= today)
            return Result.Failure("week_already_started");

        week.Pause(WeekStatus.Hibernated);
        await unitOfWork.CommitAsync(ct);
        broadcaster.Broadcast();
        return Result.Success();
    }

    private async Task<Result> FreezeAsync(
        Domain.Week.Week week, DateTime today, CancellationToken ct)
    {
        if (week.StartDate.Date > today)
            return Result.Failure("week_not_started");

        if (today.DayOfWeek >= DayOfWeek.Friday)
            return Result.Failure("freeze_window_closed");

        var profile = await userProfileRepository.GetSingleAsync(ct);
        if (profile is null)
            return Result.Failure("profile_not_found");

        if (!profile.ConsumeShield())
            return Result.Failure("no_shields");

        week.Pause(WeekStatus.Frozen);

        // Re-entry check: if the preceding week is also frozen, mark the next week as re-entry
        var prevWeek = await weekRepository.GetByWeekNumberAsync(week.WeekNumber - 1, ct);
        if (prevWeek?.Status == WeekStatus.Frozen)
        {
            var nextWeek = await weekRepository.GetByWeekNumberAsync(week.WeekNumber + 1, ct);
            nextWeek?.MarkAsReEntry();
        }

        await unitOfWork.CommitAsync(ct);
        broadcaster.Broadcast();
        return Result.Success();
    }
}
