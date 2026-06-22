using LifeGrid.Domain.Common;

namespace LifeGrid.Application.Week;

public interface IGeminiHabitGenerationService
{
    Task<Result<HabitSchedulingResult>> GenerateScheduleAsync(
        string            goalAsStated,
        string            deadlineAsStated,
        string            baselineAnswersJson,
        DateTime          startDate,
        CancellationToken ct = default);
}
