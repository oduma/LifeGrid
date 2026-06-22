using LifeGrid.Domain.Common;

namespace LifeGrid.Application.Goal;

public interface IGeminiGoalValidationService
{
    Task<Result<GeminiValidationResult>> ValidateGoalAsync(
        string rawDraft, DateTime chosenStartDate, CancellationToken ct = default);

    Task<Result<IReadOnlyList<RefinementQuestionDto>>> GenerateRefinementQuestionsAsync(
        string validatedGoalJson, CancellationToken ct = default);
}
