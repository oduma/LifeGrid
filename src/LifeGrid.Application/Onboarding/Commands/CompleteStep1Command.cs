using LifeGrid.Domain.Common;
using LifeGrid.Domain.Onboarding;
using MediatR;

namespace LifeGrid.Application.Onboarding.Commands;

public record CompleteStep1Command : IRequest<Result<OnboardingSession>>;

public sealed class CompleteStep1CommandHandler(IOnboardingRepository repository)
    : IRequestHandler<CompleteStep1Command, Result<OnboardingSession>>
{
    public async Task<Result<OnboardingSession>> Handle(
        CompleteStep1Command request,
        CancellationToken cancellationToken)
    {
        var session = await repository.GetActiveSessionAsync(cancellationToken);
        if (session is null)
            return Result<OnboardingSession>.Failure("No active onboarding session.");

        session.AdvanceToStep1();
        await repository.UpsertAsync(session, cancellationToken);
        return Result<OnboardingSession>.Success(session);
    }
}
