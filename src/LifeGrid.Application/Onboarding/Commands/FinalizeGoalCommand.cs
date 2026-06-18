using LifeGrid.Application.Goal;
using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Common;
using LifeGrid.Domain.Onboarding;
using MediatR;
using System.Text.Json;
using GoalAggregate = LifeGrid.Domain.Goal.Goal;

namespace LifeGrid.Application.Onboarding.Commands;

public record FinalizeGoalCommand(
    IReadOnlyList<(int RankOrder, string Answer)> UserAnswers)
    : IRequest<Result>;

public sealed class FinalizeGoalCommandHandler(
    IOnboardingRepository  onboardingRepository,
    IUserProfileRepository userProfileRepository,
    IGoalRepository        goalRepository)
    : IRequestHandler<FinalizeGoalCommand, Result>
{
    public async Task<Result> Handle(
        FinalizeGoalCommand request,
        CancellationToken   cancellationToken)
    {
        var session = await onboardingRepository.GetActiveSessionAsync(cancellationToken);
        if (session is null)
            return Result.Failure("No active onboarding session.");

        if (session.CurrentStep != OnboardingStep.Step1_RefinementQuestionsActive)
            return Result.Failure("Session is not in the refinement questions state.");

        if (string.IsNullOrWhiteSpace(session.ValidatedGoalJson))
            return Result.Failure("No validated goal data found in session.");

        if (string.IsNullOrWhiteSpace(session.RefinementQuestionsJson))
            return Result.Failure("No refinement questions found in session.");

        var userProfile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (userProfile is null)
            return Result.Failure("User profile not found.");

        ValidatedGoalDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ValidatedGoalDto>(session.ValidatedGoalJson);
        }
        catch (JsonException)
        {
            return Result.Failure("Validated goal data is corrupt.");
        }

        if (dto is null)
            return Result.Failure("Validated goal data could not be parsed.");

        List<RefinementQuestionDto>? questions;
        try
        {
            questions = JsonSerializer.Deserialize<List<RefinementQuestionDto>>(session.RefinementQuestionsJson);
        }
        catch (JsonException)
        {
            return Result.Failure("Refinement questions data is corrupt.");
        }

        if (questions is null)
            return Result.Failure("Refinement questions could not be parsed.");

        var goal = GoalAggregate.Create(
            userProfile.UserId,
            dto.Description,
            dto.AmbientTag,
            dto.Duration,
            dto.DeadlineDate);

        var answerLookup = request.UserAnswers.ToDictionary(a => a.RankOrder, a => a.Answer);
        var refinementItems = questions.Select(q =>
            (q.RankOrder, q.Question, answerLookup.TryGetValue(q.RankOrder, out var ans) ? ans : null));

        goal.SetRefinementAnswers(refinementItems!);

        await goalRepository.AddAsync(goal, cancellationToken);

        session.AdvanceToExecutionVerified();
        await onboardingRepository.UpsertAsync(session, cancellationToken);

        return Result.Success();
    }
}
