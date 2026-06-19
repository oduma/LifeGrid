namespace LifeGrid.Application.Vice;

public sealed record SurveyQuestionDto(
    string                    Id,
    string                    Type,
    string                    QuestionText,
    IReadOnlyList<string>?    Options);
