using LifeGrid.Domain.Common;

namespace LifeGrid.Application.MomentBurst;

public interface IGeminiMomentBurstService
{
    Task<Result<MomentBurstResult>> GenerateAsync(
        string            userFreeText,
        string            weeklyHabitsJson,
        DateTime          currentDate,
        CancellationToken ct = default);
}
