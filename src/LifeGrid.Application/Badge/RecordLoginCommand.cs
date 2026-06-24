using LifeGrid.Application.Common;
using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Badge;
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Badge;

public record RecordLoginCommand : IRequest<Result<IReadOnlyCollection<BadgeDto>>>;

public sealed class RecordLoginCommandHandler(
    IUserProfileRepository    userProfileRepository,
    ILoginHistoryRepository   loginHistoryRepository,
    IConsistencyBadgeEvaluator evaluator,
    IUnitOfWork               unitOfWork,
    IDateTimeProvider         clock)
    : IRequestHandler<RecordLoginCommand, Result<IReadOnlyCollection<BadgeDto>>>
{
    public async Task<Result<IReadOnlyCollection<BadgeDto>>> Handle(
        RecordLoginCommand request, CancellationToken cancellationToken)
    {
        var profile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (profile is null)
            return Result<IReadOnlyCollection<BadgeDto>>.Success(Array.Empty<BadgeDto>());

        var entry = LoginHistory.Create(profile.UserId, clock.UtcNow);
        await loginHistoryRepository.AddAsync(entry, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        var newBadges = await evaluator.EvaluateAsync(profile.UserId, cancellationToken);
        return Result<IReadOnlyCollection<BadgeDto>>.Success(newBadges);
    }
}
