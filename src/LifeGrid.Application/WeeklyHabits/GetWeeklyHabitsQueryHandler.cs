using LifeGrid.Application.Goal;
using LifeGrid.Application.Habit;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using MediatR;
using HabitEntity = LifeGrid.Domain.Habit.Habit;

namespace LifeGrid.Application.WeeklyHabits;

public sealed class GetWeeklyHabitsQueryHandler(
    IWeekRepository  weekRepository,
    IGoalRepository  goalRepository,
    IHabitRepository habitRepository)
    : IRequestHandler<GetWeeklyHabitsQuery, Result<WeeklyHabitsDashboardDto>>
{
    public async Task<Result<WeeklyHabitsDashboardDto>> Handle(
        GetWeeklyHabitsQuery request,
        CancellationToken    cancellationToken)
    {
        var week = await weekRepository.GetByIdAsync(request.WeekId, cancellationToken);
        if (week is null)
            return Result<WeeklyHabitsDashboardDto>.Failure("Week not found.");

        HashSet<Guid>? filterSet = request.FilterGoalIds is { Count: > 0 } f
            ? f.ToHashSet() : null;

        var weekGoals = (filterSet is null
            ? week.WeekGoals
            : week.WeekGoals.Where(wg => filterSet.Contains(wg.GoalId)))
            .ToList();

        var goalIds = weekGoals.Select(wg => wg.GoalId).Distinct().ToList();
        var goals   = await goalRepository.GetByIdsAsync(goalIds, cancellationToken);
        var descMap = goals.ToDictionary(g => g.GoalId, g => g.Description);

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
                wg.WeekGoalId,
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
