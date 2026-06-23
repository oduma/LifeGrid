using LifeGrid.Application.Common;
using LifeGrid.Application.Habit;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using MediatR;
using System.Text.Json;
using HabitEntity    = LifeGrid.Domain.Habit.Habit;
using WeekEntity     = LifeGrid.Domain.Week.Week;
using WeekGoalEntity = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.Goal;

public record RecalculateGoalScheduleCommand(
    Guid GoalId, string OverwhelmedComment) : IRequest<Result>;

public sealed class RecalculateGoalScheduleCommandHandler(
    IUserProfileRepository        userProfileRepository,
    IGoalRepository               goalRepository,
    IWeekRepository               weekRepository,
    IHabitRepository              habitRepository,
    IGeminiHabitGenerationService habitGenerationService,
    IUnitOfWork                   unitOfWork)
    : IRequestHandler<RecalculateGoalScheduleCommand, Result>
{
    public async Task<Result> Handle(
        RecalculateGoalScheduleCommand request, CancellationToken cancellationToken)
    {
        var profile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (profile is null)
            return Result.Failure("User profile not found.");

        var goal = await goalRepository.GetByIdAsync(request.GoalId, cancellationToken);
        if (goal is null)
            return Result.Failure($"Goal {request.GoalId} not found.");

        goal.ExtendDeadlineByPercent(25.0);

        var futureWeekGoals = await weekRepository.GetFutureWeekGoalsByGoalIdAsync(
            request.GoalId, DateTime.UtcNow.Date, cancellationToken);

        var weekGoalIds = futureWeekGoals.Select(wg => wg.WeekGoalId).ToList();
        await habitRepository.RemoveByWeekGoalIdsAsync(weekGoalIds, cancellationToken);
        await weekRepository.RemoveWeekGoalRangeAsync(futureWeekGoals, cancellationToken);

        var baselineJson = BuildBaselineJson(goal, request.OverwhelmedComment);

        var recalcStartDate = Domain.Goal.Goal.CalculateStartDate(DateTime.UtcNow);

        var serviceResult = await habitGenerationService.GenerateScheduleAsync(
            goal.Description,
            goal.DeadlineDate.ToString("yyyy-MM-dd"),
            baselineJson,
            recalcStartDate,
            cancellationToken);

        if (!serviceResult.IsSuccess)
            return Result.Failure(serviceResult.Error!);

        if (serviceResult.Value is HabitSchedulingResult.Infeasible infeasible)
            return Result.Failure(infeasible.RecalibrationReason);

        var feasible = (HabitSchedulingResult.Feasible)serviceResult.Value!;

        var maxWeekGoalNumber = await weekRepository.GetMaxWeekGoalNumberAsync(
            request.GoalId, cancellationToken);

        int weekIndex = 0;
        foreach (var weekDto in feasible.Schedule)
        {
            weekIndex++;
            var existingWeek = await weekRepository.GetByStartDateAsync(weekDto.StartDate, cancellationToken);
            WeekGoalEntity weekGoal;

            if (existingWeek is null)
            {
                var newWeek = WeekEntity.Create(weekDto.WeekNumber, weekDto.StartDate);
                weekGoal    = WeekGoalEntity.Create(newWeek.WeekId, goal.GoalId, maxWeekGoalNumber + weekIndex);
                await weekRepository.AddAsync(newWeek, weekGoal, cancellationToken);
            }
            else
            {
                weekGoal = WeekGoalEntity.Create(existingWeek.WeekId, goal.GoalId, maxWeekGoalNumber + weekIndex);
                await weekRepository.AddWeekGoalAsync(weekGoal, cancellationToken);
            }

            var weekDeadline = weekDto.StartDate.AddDays(6);
            var habits = weekDto.Habits
                .Select(h => HabitEntity.Create(
                    weekGoal.WeekGoalId, Domain.Habit.HabitType.Planned,
                    h.Description, h.Description, h.Value, h.Unit, weekDeadline))
                .ToList();

            await habitRepository.AddRangeAsync(habits, cancellationToken);
        }

        profile.DeductXp(100);

        await unitOfWork.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private static string BuildBaselineJson(Domain.Goal.Goal goal, string overwhelmedComment)
    {
        var items = goal.RefinementAnswers
            .OrderBy(a => a.RankOrder)
            .Select(a => new { question = a.Question, answer = a.Answer ?? string.Empty })
            .Cast<object>()
            .ToList();

        items.Add(new { question = "Why are you overwhelmed?", answer = overwhelmedComment });

        return JsonSerializer.Serialize(items);
    }
}
