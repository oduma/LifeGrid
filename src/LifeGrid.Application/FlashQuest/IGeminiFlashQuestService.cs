using LifeGrid.Domain.Common;

namespace LifeGrid.Application.FlashQuest;

public interface IGeminiFlashQuestService
{
    Task<Result<FlashQuestGenerationResult>> GenerateAsync(
        string            weeklyHabitsJson,
        DateTime          currentDate,
        CancellationToken ct = default);
}
