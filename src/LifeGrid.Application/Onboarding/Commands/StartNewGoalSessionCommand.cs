using LifeGrid.Domain.Common;
using LifeGrid.Domain.Onboarding;
using MediatR;

namespace LifeGrid.Application.Onboarding.Commands;

public record StartNewGoalSessionCommand : IRequest<Result>;

public sealed class StartNewGoalSessionCommandHandler(IOnboardingRepository repository)
    : IRequestHandler<StartNewGoalSessionCommand, Result>
{
    public async Task<Result> Handle(
        StartNewGoalSessionCommand request,
        CancellationToken          cancellationToken)
    {
        var session = OnboardingSession.Create();
        await repository.UpsertAsync(session, cancellationToken);
        return Result.Success();
    }
}
