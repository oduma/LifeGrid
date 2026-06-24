using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Badge;

public record GetUserBadgesQuery : IRequest<Result<IReadOnlyCollection<BadgeDto>>>;

public sealed class GetUserBadgesQueryHandler(
    IBadgeRepository       badgeRepository,
    IUserProfileRepository userProfileRepository)
    : IRequestHandler<GetUserBadgesQuery, Result<IReadOnlyCollection<BadgeDto>>>
{
    public async Task<Result<IReadOnlyCollection<BadgeDto>>> Handle(
        GetUserBadgesQuery request, CancellationToken cancellationToken)
    {
        var profile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (profile is null)
            return Result<IReadOnlyCollection<BadgeDto>>.Success(Array.Empty<BadgeDto>());

        var badges = await badgeRepository.GetEarnedByUserIdAsync(profile.UserId, cancellationToken);
        if (!badges.Any())
            return Result<IReadOnlyCollection<BadgeDto>>.Success(Array.Empty<BadgeDto>());

        var dtos = badges
            .Select(b => new BadgeDto(
                b.BadgeId, b.BadgeType, b.BadgeName, b.IconName,
                b.Description, b.Tier, b.IsEarned, b.DateEarned))
            .ToArray();

        return Result<IReadOnlyCollection<BadgeDto>>.Success(dtos);
    }
}
