using LifeGrid.Application.Notification;
using Microsoft.EntityFrameworkCore;
using NotificationEntity = LifeGrid.Domain.Notification.Notification;

namespace LifeGrid.Infrastructure.Data.Repositories;

internal sealed class NotificationRepository(LifeGridDbContext db) : INotificationRepository
{
    public Task AddAsync(NotificationEntity notification, CancellationToken ct = default)
    {
        db.Notifications.Add(notification);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<NotificationEntity>> GetAllAsync(CancellationToken ct = default)
        => await db.Notifications
            .OrderByDescending(n => n.Timestamp)
            .ToListAsync(ct);

    public Task<NotificationEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Notifications.FirstOrDefaultAsync(n => n.NotificationId == id, ct);

    public Task<int> GetUnreadCountAsync(CancellationToken ct = default)
        => db.Notifications.CountAsync(n => !n.IsRead, ct);
}
