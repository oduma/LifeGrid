using LifeGrid.Domain.Common;

namespace LifeGrid.Application.ViceCheck;

public interface IGeminiViceCheckService
{
    Task<Result<GenerateQuestionResult>> GenerateQuestionAsync(
        string goalAndHabitJson, CancellationToken ct = default);

    Task<Result<EvaluateAnswerResult>> EvaluateAnswerAsync(
        string userResponseJson, CancellationToken ct = default);
}

public record GenerateQuestionResult(string Question);

public record EvaluateAnswerResult(bool Persists);
