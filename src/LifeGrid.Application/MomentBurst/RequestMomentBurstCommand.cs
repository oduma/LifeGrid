using LifeGrid.Application.Common;
using LifeGrid.Application.Habit;
using LifeGrid.Application.Week;
using LifeGrid.Application.WeeklyHabits;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.Habit;
using MediatR;
using System.Text.Json;
using HabitEntity = LifeGrid.Domain.Habit.Habit;

namespace LifeGrid.Application.MomentBurst;

public record RequestMomentBurstCommand(
    Guid   WeekGoalId,
    string UserFreeText)
    : IRequest<Result<MomentBurstOutcome>>;

public sealed class RequestMomentBurstCommandHandler(
    IWeekRepository           weekRepository,
    IHabitRepository          habitRepository,
    IGeminiMomentBurstService momentBurstService,
    IDateTimeProvider         clock,
    IUnitOfWork               unitOfWork)
    : IRequestHandler<RequestMomentBurstCommand, Result<MomentBurstOutcome>>
{
    public async Task<Result<MomentBurstOutcome>> Handle(
        RequestMomentBurstCommand command,
        CancellationToken         cancellationToken)
    {
        var weekGoal = await weekRepository.GetWeekGoalByIdAsync(command.WeekGoalId, cancellationToken);
        if (weekGoal is null)
            return Result<MomentBurstOutcome>.Failure("WeekGoal not found.");

        var week = await weekRepository.GetByIdAsync(weekGoal.WeekId, cancellationToken);
        if (week is null)
            return Result<MomentBurstOutcome>.Failure("Week not found.");

        var today       = clock.UtcNow.Date;
        int daysFromMon = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var currentMonday = today.AddDays(-daysFromMon);
        // Use DateOnly to compare without being tripped up by DateTimeKind.Unspecified
        // returned from SQLite vs. DateTimeKind.Utc from the clock.
        if (DateOnly.FromDateTime(week.StartDate) != DateOnly.FromDateTime(currentMonday))
            return Result<MomentBurstOutcome>.Failure("Not the current week.");

        if (weekGoal.GoalWeeklyGp < 100.0)
            return Result<MomentBurstOutcome>.Failure(
                "Goal progress must be 100% to request a Moment Burst.");

        var habits     = await habitRepository.GetByWeekGoalIdAsync(command.WeekGoalId, cancellationToken);
        var habitsJson = BuildHabitsJson(habits);

        var aiResult = await momentBurstService.GenerateAsync(
            command.UserFreeText, habitsJson, today, cancellationToken);

        if (!aiResult.IsSuccess)
            return Result<MomentBurstOutcome>.Failure(aiResult.Error!);

        if (aiResult.Value is MomentBurstResult.Denied denied)
            return Result<MomentBurstOutcome>.Success(
                new MomentBurstOutcome.Denied(denied.Message));

        var accepted = (MomentBurstResult.Accepted)aiResult.Value!;
        var deadline = DateTime.SpecifyKind(week.StartDate.AddDays(6), DateTimeKind.Utc);
        var habit    = HabitEntity.Create(
            command.WeekGoalId,
            HabitType.MomentBurst,
            accepted.QuestName,
            accepted.Description,
            accepted.MeasureValue,
            accepted.MeasureUnit,
            deadline);

        await habitRepository.AddRangeAsync([habit], cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        var itemDto = new WeeklyHabitItemDto(
            habit.HabitId,
            habit.HabitType.ToString(),
            habit.HabitName,
            habit.HabitDescription,
            habit.TargetValue,
            habit.MeasurementUnit,
            habit.DeadlineDateTime,
            []);

        return Result<MomentBurstOutcome>.Success(
            new MomentBurstOutcome.HabitCreated(itemDto));
    }

    private static string BuildHabitsJson(IReadOnlyList<HabitEntity> habits)
    {
        var items = habits.Select(h => new
        {
            habit_name = h.HabitName,
            target_measurement = new
            {
                value = h.TargetValue,
                unit  = h.MeasurementUnit
            },
            complete_measurement = new
            {
                value = h.CompletedValuesLog.Sum(l => l.ActualValue),
                unit  = h.MeasurementUnit
            }
        });
        return JsonSerializer.Serialize(items);
    }
}
