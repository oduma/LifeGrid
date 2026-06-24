using LifeGrid.Application.Badge;
using Microsoft.EntityFrameworkCore;
using BadgeEntity = LifeGrid.Domain.Badge.Badge;

namespace LifeGrid.Infrastructure.Data.Repositories;

internal sealed class BadgeRepository(LifeGridDbContext db) : IBadgeRepository
{
    public Task AddAsync(BadgeEntity badge, CancellationToken ct = default)
    {
        db.Badges.Add(badge);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<BadgeEntity>> GetEarnedByUserIdAsync(
        Guid userId, CancellationToken ct = default)
        => await db.Badges
            .Where(b => b.UserId == userId && b.IsEarned)
            .ToListAsync(ct);
}
