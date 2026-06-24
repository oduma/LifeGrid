using BadgeEntity = LifeGrid.Domain.Badge.Badge;

namespace LifeGrid.Application.Badge;

public interface IBadgeRepository
{
    Task AddAsync(BadgeEntity badge, CancellationToken ct = default);
    Task<IReadOnlyList<BadgeEntity>> GetEarnedByUserIdAsync(Guid userId, CancellationToken ct = default);
}
