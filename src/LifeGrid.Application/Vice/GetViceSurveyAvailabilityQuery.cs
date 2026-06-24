using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Vice;

public record GetViceSurveyAvailabilityQuery : IRequest<Result<bool>>;

public sealed class GetViceSurveyAvailabilityQueryHandler(
    IUserProfileRepository userProfileRepository)
    : IRequestHandler<GetViceSurveyAvailabilityQuery, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        GetViceSurveyAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var profile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (profile is null || profile.IsViceSurveyCompleted)
            return Result<bool>.Success(false);
        return Result<bool>.Success(true);
    }
}
