using UserProfileAggregate = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Application.UserProfile;

public interface IUserProfileRepository
{
    Task<UserProfileAggregate?> GetSingleAsync(CancellationToken ct = default);
    Task AddAsync(UserProfileAggregate profile, CancellationToken ct = default);
}
