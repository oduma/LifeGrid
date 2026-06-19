using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Hud;

public record GetHudTelemetryQuery : IRequest<Result<HudTelemetryDto>>;

public sealed class GetHudTelemetryQueryHandler(
    IUserProfileRepository userProfileRepository,
    IWeekRepository        weekRepository)
    : IRequestHandler<GetHudTelemetryQuery, Result<HudTelemetryDto>>
{
    public async Task<Result<HudTelemetryDto>> Handle(
        GetHudTelemetryQuery request,
        CancellationToken    cancellationToken)
    {
        var profile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (profile is null)
            return Result<HudTelemetryDto>.Success(new HudTelemetryDto(0, 0.0, 0.0, 0, 0, 0, 0, 0, 2));

        var week = await weekRepository.GetActiveAsync(cancellationToken);
        if (week is null)
            return Result<HudTelemetryDto>.Success(new HudTelemetryDto(
                profile.CurrentLevel,
                profile.Economy.LifetimeGpAverage,
                0.0,
                profile.Economy.LifetimeXp,
                0,
                profile.Economy.CurrentSp,
                0,
                profile.Economy.ShieldsAvailable,
                profile.Economy.MaxShieldCap));

        var weekGoals = week.WeekGoals.ToList();
        var weeklyGp  = weekGoals.Count > 0 ? weekGoals.Average(wg => wg.GoalWeeklyGp) : 0.0;
        var weeklyXp  = weekGoals.Sum(wg => wg.GoalWeeklyXpEarned);

        return Result<HudTelemetryDto>.Success(new HudTelemetryDto(
            profile.CurrentLevel,
            profile.Economy.LifetimeGpAverage,
            weeklyGp,
            profile.Economy.LifetimeXp,
            weeklyXp,
            profile.Economy.CurrentSp,
            week.TotalWeeklySpEarned,
            profile.Economy.ShieldsAvailable,
            profile.Economy.MaxShieldCap));
    }
}
