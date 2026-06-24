using LoginHistoryEntity = LifeGrid.Domain.Badge.LoginHistory;

namespace LifeGrid.Application.Badge;

public interface ILoginHistoryRepository
{
    Task AddAsync(LoginHistoryEntity entry, CancellationToken ct = default);
    Task<IReadOnlyList<DateTime>> GetTimestampsByUserIdAsync(Guid userId, CancellationToken ct = default);
}
