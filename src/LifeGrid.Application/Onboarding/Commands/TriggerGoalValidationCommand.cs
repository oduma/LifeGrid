using LifeGrid.Application.Goal;
using LifeGrid.Domain.Common;
using MediatR;
using System.Text.Json;

namespace LifeGrid.Application.Onboarding.Commands;

public record TriggerGoalValidationCommand : IRequest<Result<IReadOnlyList<RefinementQuestionDto>>>;

public sealed class TriggerGoalValidationCommandHandler(
    IOnboardingRepository           repository,
    IGeminiGoalValidationService    gemini)
    : IRequestHandler<TriggerGoalValidationCommand, Result<IReadOnlyList<RefinementQuestionDto>>>
{
    public async Task<Result<IReadOnlyList<RefinementQuestionDto>>> Handle(
        TriggerGoalValidationCommand request,
        CancellationToken            cancellationToken)
    {
        var session = await repository.GetActiveSessionAsync(cancellationToken);
        if (session is null)
            return Result<IReadOnlyList<RefinementQuestionDto>>.Failure("No active onboarding session.");

        if (string.IsNullOrWhiteSpace(session.RawGoalDraft))
            return Result<IReadOnlyList<RefinementQuestionDto>>.Failure("Goal draft is empty.");

        session.AdvanceToAwaitingValidation();
        await repository.UpsertAsync(session, cancellationToken);

        var validationResult = await gemini.ValidateGoalAsync(session.RawGoalDraft, cancellationToken);
        if (!validationResult.IsSuccess)
        {
            session.RevertToGoalDraftCaptured();
            await repository.UpsertAsync(session, cancellationToken);
            return Result<IReadOnlyList<RefinementQuestionDto>>.Failure(validationResult.Error!);
        }

        if (validationResult.Value is GeminiValidationResult.Invalid invalid)
        {
            session.RevertToGoalDraftCaptured();
            await repository.UpsertAsync(session, cancellationToken);
            return Result<IReadOnlyList<RefinementQuestionDto>>.Failure(invalid.RetryPrompt);
        }

        var validData        = ((GeminiValidationResult.Valid)validationResult.Value!).Data;
        var validatedGoalJson = JsonSerializer.Serialize(validData);

        var questionsResult = await gemini.GenerateRefinementQuestionsAsync(validatedGoalJson, cancellationToken);
        if (!questionsResult.IsSuccess)
        {
            session.RevertToGoalDraftCaptured();
            await repository.UpsertAsync(session, cancellationToken);
            return Result<IReadOnlyList<RefinementQuestionDto>>.Failure(questionsResult.Error!);
        }

        var questionsJson = JsonSerializer.Serialize(questionsResult.Value);
        session.AdvanceToRefinementQuestionsActive(validatedGoalJson, questionsJson);
        await repository.UpsertAsync(session, cancellationToken);

        return Result<IReadOnlyList<RefinementQuestionDto>>.Success(questionsResult.Value!);
    }
}
