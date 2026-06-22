using LifeGrid.Domain.Common;

namespace LifeGrid.Application.Week;

public interface IGeminiHabitGenerationService
{
    // Combined call (prompt 2.1 + 2.2) — used by RecalculateGoalScheduleCommand
    Task<Result<HabitSchedulingResult>> GenerateScheduleAsync(
        string            goalAsStated,
        string            deadlineAsStated,
        string            baselineAnswersJson,
        DateTime          startDate,
        CancellationToken ct = default);

    // Phase 1 only: prompt 2.1 — used by GenerateBlueprintCommand
    Task<Result<BlueprintResult>> GenerateBlueprintAsync(
        string            goalAsStated,
        string            deadlineAsStated,
        string            baselineAnswersJson,
        DateTime          startDate,
        CancellationToken ct = default);

    // Phase 2 only: prompt 2.2 — used by GenerateScheduleCommand
    Task<Result<HabitSchedulingResult>> GenerateScheduleFromBlueprintAsync(
        string            blueprintJson,
        DateTime          startDate,
        CancellationToken ct = default);
}
