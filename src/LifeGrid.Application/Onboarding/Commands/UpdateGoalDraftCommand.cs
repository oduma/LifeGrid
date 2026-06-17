using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Onboarding.Commands;

public record UpdateGoalDraftCommand(string Draft) : IRequest<Result>;

public sealed class UpdateGoalDraftCommandHandler(IOnboardingRepository repository)
    : IRequestHandler<UpdateGoalDraftCommand, Result>
{
    public async Task<Result> Handle(
        UpdateGoalDraftCommand request,
        CancellationToken cancellationToken)
    {
        var session = await repository.GetActiveSessionAsync(cancellationToken);
        if (session is null)
            return Result.Failure("No active onboarding session.");

        session.UpdateDraft(request.Draft);
        await repository.UpsertAsync(session, cancellationToken);
        return Result.Success();
    }
}
