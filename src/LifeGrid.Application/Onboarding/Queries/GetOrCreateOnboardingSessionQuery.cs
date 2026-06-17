using LifeGrid.Domain.Common;
using LifeGrid.Domain.Onboarding;
using MediatR;

namespace LifeGrid.Application.Onboarding.Queries;

public record GetOrCreateOnboardingSessionQuery : IRequest<Result<OnboardingSession>>;

public sealed class GetOrCreateOnboardingSessionQueryHandler(IOnboardingRepository repository)
    : IRequestHandler<GetOrCreateOnboardingSessionQuery, Result<OnboardingSession>>
{
    public async Task<Result<OnboardingSession>> Handle(
        GetOrCreateOnboardingSessionQuery request,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetActiveSessionAsync(cancellationToken);
        if (existing is not null)
            return Result<OnboardingSession>.Success(existing);

        var session = OnboardingSession.Create();
        await repository.UpsertAsync(session, cancellationToken);
        return Result<OnboardingSession>.Success(session);
    }
}
