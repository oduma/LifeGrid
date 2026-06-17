using LifeGrid.Domain.Common;
using MediatR;
using UserProfileAggregate = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Application.UserProfile.Queries;

public record GetOrCreateUserProfileQuery : IRequest<Result<UserProfileAggregate>>;

public sealed class GetOrCreateUserProfileQueryHandler(IUserProfileRepository repository)
    : IRequestHandler<GetOrCreateUserProfileQuery, Result<UserProfileAggregate>>
{
    public async Task<Result<UserProfileAggregate>> Handle(
        GetOrCreateUserProfileQuery request,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetSingleAsync(cancellationToken);
        if (existing is not null)
            return Result<UserProfileAggregate>.Success(existing);

        var profile = UserProfileAggregate.Create();
        await repository.AddAsync(profile, cancellationToken);
        return Result<UserProfileAggregate>.Success(profile);
    }
}
