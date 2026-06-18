using LifeGrid.Domain.Common;
using LifeGrid.Domain.Onboarding;
using MediatR;

namespace LifeGrid.Application.Onboarding.Commands;

public record SaveRefinementAnswersCommand(string AnswersJson) : IRequest<Result>;

public sealed class SaveRefinementAnswersCommandHandler(IOnboardingRepository onboardingRepository)
    : IRequestHandler<SaveRefinementAnswersCommand, Result>
{
    public async Task<Result> Handle(
        SaveRefinementAnswersCommand request,
        CancellationToken            cancellationToken)
    {
        var session = await onboardingRepository.GetActiveSessionAsync(cancellationToken);
        if (session is null)
            return Result.Failure("No active onboarding session.");

        if (session.CurrentStep != OnboardingStep.Step1_RefinementQuestionsActive)
            return Result.Failure("Session is not in the refinement questions state.");

        session.SaveRefinementAnswers(request.AnswersJson);
        await onboardingRepository.UpsertAsync(session, cancellationToken);
        return Result.Success();
    }
}
