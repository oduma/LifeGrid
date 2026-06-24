using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Badge;

public record GetUserBadgesQuery : IRequest<Result<IReadOnlyCollection<BadgeDto>>>;

public sealed class GetUserBadgesQueryHandler(IUserProfileRepository userProfileRepository)
    : IRequestHandler<GetUserBadgesQuery, Result<IReadOnlyCollection<BadgeDto>>>
{
    public async Task<Result<IReadOnlyCollection<BadgeDto>>> Handle(
        GetUserBadgesQuery request, CancellationToken cancellationToken)
    {
        var profile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (profile is null || !profile.Badges.Any())
            return Result<IReadOnlyCollection<BadgeDto>>.Success(Array.Empty<BadgeDto>());

        var dtos = profile.Badges
            .Select(b => new BadgeDto(b.BadgeId, b.BadgeType, b.IconName, b.Description, b.DateEarned))
            .ToArray();

        return Result<IReadOnlyCollection<BadgeDto>>.Success(dtos);
    }
}
