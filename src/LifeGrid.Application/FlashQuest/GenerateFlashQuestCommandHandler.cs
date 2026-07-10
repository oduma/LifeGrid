using System.Text.Json;
using System.Text.Json.Serialization;
using LifeGrid.Application.Common;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Habit;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.Habit;
using MediatR;
using HabitEntity = LifeGrid.Domain.Habit.Habit;

namespace LifeGrid.Application.FlashQuest;

public sealed class GenerateFlashQuestCommandHandler(
    IWeekRepository         weekRepository,
    IHabitRepository        habitRepository,
    IGoalRepository         goalRepository,
    IGeminiFlashQuestService geminiFlashQuestService,
    IDateTimeProvider       dateTimeProvider,
    IUnitOfWork             unitOfWork)
    : IRequestHandler<GenerateFlashQuestCommand, Result<GenerateFlashQuestResult>>
{
    public async Task<Result<GenerateFlashQuestResult>> Handle(
        GenerateFlashQuestCommand request, CancellationToken cancellationToken)
    {
        var week = await weekRepository.GetByIdAsync(request.WeekId, cancellationToken);
        if (week is null)
            return Result<GenerateFlashQuestResult>.Failure("week_not_found");

        if (await habitRepository.HasFlashHabitsInWeekAsync(week.WeekId, cancellationToken))
            return Result<GenerateFlashQuestResult>.Success(new GenerateFlashQuestResult(0));

        var laggingGoals = week.WeekGoals.Where(wg => wg.GoalWeeklyGp < 50.0).ToList();
        if (laggingGoals.Count == 0)
            return Result<GenerateFlashQuestResult>.Success(new GenerateFlashQuestResult(0));

        var payloadItems     = new List<FlashQuestPayloadItem>();
        var habitToWeekGoal  = new Dictionary<Guid, Guid>();

        foreach (var weekGoal in laggingGoals)
        {
            var goal = await goalRepository.GetByIdAsync(weekGoal.GoalId, cancellationToken);
            var summaries = await habitRepository.GetCompletionSummariesForWeekGoalAsync(
                weekGoal.WeekGoalId, cancellationToken);
            var habits = await habitRepository.GetByWeekGoalIdAsync(
                weekGoal.WeekGoalId, cancellationToken);
            var habitsById = habits.ToDictionary(h => h.HabitId);

            foreach (var summary in summaries)
            {
                if (!habitsById.TryGetValue(summary.HabitId, out var habit))
                    continue;

                habitToWeekGoal[summary.HabitId] = weekGoal.WeekGoalId;
                payloadItems.Add(new FlashQuestPayloadItem(
                    summary.HabitId,
                    goal?.Description ?? string.Empty,
                    habit.HabitName,
                    habit.HabitDescription,
                    habit.HabitType.ToString(),
                    new FlashQuestMeasure(summary.TotalActualValue, habit.MeasurementUnit),
                    new FlashQuestMeasure(summary.TargetValue, habit.MeasurementUnit)));
            }
        }

        var payloadJson = JsonSerializer.Serialize(payloadItems, JsonOpts);
        var aiResult    = await geminiFlashQuestService.GenerateAsync(
            payloadJson, dateTimeProvider.UtcNow, cancellationToken);

        if (!aiResult.IsSuccess || aiResult.Value is FlashQuestGenerationResult.NotEligible)
            return Result<GenerateFlashQuestResult>.Success(new GenerateFlashQuestResult(0));

        var accepted   = (FlashQuestGenerationResult.Accepted)aiResult.Value!;
        var injectedAt = dateTimeProvider.UtcNow;
        var newHabits  = new List<HabitEntity>();

        foreach (var quest in accepted.Quests)
        {
            if (!habitToWeekGoal.TryGetValue(quest.SourceHabitId, out var weekGoalId))
                continue;

            newHabits.Add(HabitEntity.Create(
                weekGoalId,
                HabitType.Flash,
                quest.QuestName,
                quest.Description,
                quest.MeasureValue,
                quest.MeasureUnit,
                injectedAt.AddHours(24)));
        }

        if (newHabits.Count == 0)
            return Result<GenerateFlashQuestResult>.Success(new GenerateFlashQuestResult(0));

        await habitRepository.AddRangeAsync(newHabits, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<GenerateFlashQuestResult>.Success(new GenerateFlashQuestResult(newHabits.Count));
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private record FlashQuestPayloadItem(
        [property: JsonPropertyName("habit_id")]          Guid   HabitId,
        [property: JsonPropertyName("goal_description")]  string GoalDescription,
        [property: JsonPropertyName("habit_name")]        string HabitName,
        [property: JsonPropertyName("habit_description")] string HabitDescription,
        [property: JsonPropertyName("habit_type")]        string HabitType,
        [property: JsonPropertyName("complete_measurement")] FlashQuestMeasure CompleteMeasurement,
        [property: JsonPropertyName("target_measurement")]   FlashQuestMeasure TargetMeasurement);

    private record FlashQuestMeasure(double Value, string Unit);
}
