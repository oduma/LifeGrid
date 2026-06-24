using LifeGrid.Application.Badge;
using Microsoft.EntityFrameworkCore;
using LoginHistoryEntity = LifeGrid.Domain.Badge.LoginHistory;

namespace LifeGrid.Infrastructure.Data.Repositories;

internal sealed class LoginHistoryRepository(LifeGridDbContext db) : ILoginHistoryRepository
{
    public Task AddAsync(LoginHistoryEntity entry, CancellationToken ct = default)
    {
        db.LoginHistory.Add(entry);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<DateTime>> GetTimestampsByUserIdAsync(
        Guid userId, CancellationToken ct = default)
        => await db.LoginHistory
            .Where(l => l.UserId == userId)
            .Select(l => l.Timestamp)
            .ToListAsync(ct);
}
