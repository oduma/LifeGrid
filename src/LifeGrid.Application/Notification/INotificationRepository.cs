using NotificationEntity = LifeGrid.Domain.Notification.Notification;

namespace LifeGrid.Application.Notification;

public interface INotificationRepository
{
    Task AddAsync(NotificationEntity notification, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationEntity>> GetAllAsync(CancellationToken ct = default);
    Task<NotificationEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
}
