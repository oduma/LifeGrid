using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Goal;

public record GetActiveGoalCountQuery : IRequest<Result<int>>;

public sealed class GetActiveGoalCountQueryHandler(
    IUserProfileRepository userProfileRepository,
    IGoalRepository        goalRepository)
    : IRequestHandler<GetActiveGoalCountQuery, Result<int>>
{
    public async Task<Result<int>> Handle(
        GetActiveGoalCountQuery request,
        CancellationToken       cancellationToken)
    {
        var profile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (profile is null)
            return Result<int>.Success(0);

        var count = await goalRepository.GetActiveCountAsync(profile.UserId, cancellationToken);
        return Result<int>.Success(count);
    }
}
