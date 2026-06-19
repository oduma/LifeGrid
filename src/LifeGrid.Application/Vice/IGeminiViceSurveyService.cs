using LifeGrid.Domain.Common;

namespace LifeGrid.Application.Vice;

public interface IGeminiViceSurveyService
{
    Task<Result<IReadOnlyList<SurveyQuestionDto>>> GenerateQuestionsAsync(
        string goalsContextJson, CancellationToken ct = default);

    Task<Result<IReadOnlyList<DetectedViceDto>>> AnalyzeAnswersAsync(
        string answersJson, string goalsJson, CancellationToken ct = default);
}
