using LifeGrid.Application.Goal;
using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Common;
using MediatR;
using System.Text.Json;

namespace LifeGrid.Application.Vice;

public record GetViceSurveyQuestionsQuery : IRequest<Result<IReadOnlyList<SurveyQuestionDto>>>;

public sealed class GetViceSurveyQuestionsQueryHandler(
    IUserProfileRepository  userProfiles,
    IGoalRepository         goals,
    IGeminiViceSurveyService viceSurveyService)
    : IRequestHandler<GetViceSurveyQuestionsQuery, Result<IReadOnlyList<SurveyQuestionDto>>>
{
    public async Task<Result<IReadOnlyList<SurveyQuestionDto>>> Handle(
        GetViceSurveyQuestionsQuery request,
        CancellationToken           cancellationToken)
    {
        var profile      = await userProfiles.GetSingleAsync(cancellationToken);
        var activeGoals  = profile is null
            ? []
            : await goals.GetAllByUserIdAsync(profile.UserId, cancellationToken);

        var goalsContextJson = JsonSerializer.Serialize(
            activeGoals.Select(g => new
            {
                description = g.Description,
                ambientTag  = g.AmbientTag,
                deadlineDate = g.DeadlineDate.ToString("yyyy-MM-dd")
            }));

        return await viceSurveyService.GenerateQuestionsAsync(goalsContextJson, cancellationToken);
    }
}
