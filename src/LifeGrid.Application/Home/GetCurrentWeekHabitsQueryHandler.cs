using LifeGrid.Application.Common;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Habit;
using LifeGrid.Application.Week;
using LifeGrid.Application.WeeklyHabits;
using LifeGrid.Domain.Common;
using MediatR;
using HabitEntity = LifeGrid.Domain.Habit.Habit;

namespace LifeGrid.Application.Home;

public sealed class GetCurrentWeekHabitsQueryHandler(
    IWeekRepository   weekRepository,
    IGoalRepository   goalRepository,
    IHabitRepository  habitRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetCurrentWeekHabitsQuery, Result<WeeklyHabitsDashboardDto>>
{
    public async Task<Result<WeeklyHabitsDashboardDto>> Handle(
        GetCurrentWeekHabitsQuery request,
        CancellationToken         cancellationToken)
    {
        var today         = dateTimeProvider.UtcNow.Date;
        int daysFromMon   = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var currentMonday = today.AddDays(-daysFromMon);

        var week = await weekRepository.GetByStartDateAsync(currentMonday, cancellationToken);
        if (week is null)
            return Result<WeeklyHabitsDashboardDto>.Failure("No active week found.");

        var weekGoals    = week.WeekGoals.ToList();
        var goalIds      = weekGoals.Select(wg => wg.GoalId).Distinct().ToList();
        var goals        = await goalRepository.GetByIdsAsync(goalIds, cancellationToken);
        var descMap      = goals.ToDictionary(g => g.GoalId, g => g.Description);

        var weekGoalIds  = weekGoals.Select(wg => wg.WeekGoalId).ToList();
        var habits       = await habitRepository.GetByWeekGoalIdsAsync(weekGoalIds, cancellationToken);
        var habitsByWgId = habits
            .GroupBy(h => h.WeekGoalId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var groups = weekGoals.Select(wg =>
        {
            var wgHabits = habitsByWgId.TryGetValue(wg.WeekGoalId, out var list)
                ? list : new List<HabitEntity>();

            return new WeeklyGoalGroupDto(
                wg.GoalId,
                descMap.GetValueOrDefault(wg.GoalId, string.Empty),
                wg.WeekGoalNumber,
                wg.PenaltyState.ToString(),
                wg.GoalWeeklyGp,
                wg.GoalWeeklyXpEarned,
                wgHabits.Select(h => new WeeklyHabitItemDto(
                    h.HabitId,
                    h.HabitType.ToString(),
                    h.HabitName,
                    h.HabitDescription,
                    h.TargetValue,
                    h.MeasurementUnit,
                    h.DeadlineDateTime,
                    h.CompletedValuesLog.Select(l => new HabitCompletionLogDto(
                        l.LogId,
                        l.ActualValue,
                        l.MeasurementUnit,
                        l.ProofText,
                        l.ProofImageUrl,
                        l.Timestamp)).ToList()
                )).ToList());
        }).ToList();

        return Result<WeeklyHabitsDashboardDto>.Success(new WeeklyHabitsDashboardDto(
            week.WeekId,
            week.StartDate,
            week.Status.ToString(),
            week.TotalWeeklySpEarned,
            groups));
    }
}
