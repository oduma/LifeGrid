using LifeGrid.Application.Common;
using LifeGrid.Application.Goal;
using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Common;
using MediatR;
using System.Text.Json;

namespace LifeGrid.Application.Vice;

public record SubmitViceSurveyCommand(IReadOnlyList<SurveyAnswerDto> Answers)
    : IRequest<Result<IReadOnlyList<DetectedViceDto>>>;

public sealed class SubmitViceSurveyCommandHandler(
    IUserProfileRepository   userProfiles,
    IGoalRepository          goals,
    IGeminiViceSurveyService viceSurveyService,
    IUnitOfWork              unitOfWork)
    : IRequestHandler<SubmitViceSurveyCommand, Result<IReadOnlyList<DetectedViceDto>>>
{
    public async Task<Result<IReadOnlyList<DetectedViceDto>>> Handle(
        SubmitViceSurveyCommand request,
        CancellationToken       cancellationToken)
    {
        var profile = await userProfiles.GetSingleAsync(cancellationToken);
        if (profile is null)
            return Result<IReadOnlyList<DetectedViceDto>>.Failure("No user profile found.");

        if (profile.IsViceSurveyCompleted)
            return Result<IReadOnlyList<DetectedViceDto>>.Failure("Vice survey already completed.");

        var activeGoals = await goals.GetAllByUserIdAsync(profile.UserId, cancellationToken);

        var answersJson = JsonSerializer.Serialize(
            request.Answers.Select(a => new { questionId = a.QuestionId, answerText = a.AnswerText }));

        var goalsJson = JsonSerializer.Serialize(
            activeGoals.Select(g => new
            {
                description  = g.Description,
                ambientTag   = g.AmbientTag,
                deadlineDate = g.DeadlineDate.ToString("yyyy-MM-dd")
            }));

        var analysisResult = await viceSurveyService.AnalyzeAnswersAsync(
            answersJson, goalsJson, cancellationToken);

        if (!analysisResult.IsSuccess)
            return Result<IReadOnlyList<DetectedViceDto>>.Failure(analysisResult.Error!);

        var allVices = analysisResult.Value!;

        var vicesByGoal = allVices
            .GroupBy(v => v.GoalDescription, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.AsEnumerable(), StringComparer.OrdinalIgnoreCase);

        foreach (var goal in activeGoals)
        {
            if (vicesByGoal.TryGetValue(goal.Description, out var goalVices))
                goal.SetLinkedBadHabits(goalVices.Select(v => (v.Description, v.DangerLevel)));
        }

        profile.GrantSurveyBonusShield();
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<IReadOnlyList<DetectedViceDto>>.Success(allVices);
    }
}
