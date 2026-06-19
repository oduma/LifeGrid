using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Vice;

public record LaunchViceSurveyCommand : IRequest<Result>;

public sealed class LaunchViceSurveyCommandHandler(IUserProfileRepository userProfiles)
    : IRequestHandler<LaunchViceSurveyCommand, Result>
{
    public async Task<Result> Handle(LaunchViceSurveyCommand request, CancellationToken cancellationToken)
    {
        var profile = await userProfiles.GetSingleAsync(cancellationToken);
        if (profile is null)
            return Result.Success();

        return profile.IsViceSurveyCompleted
            ? Result.Failure("already_completed")
            : Result.Success();
    }
}
