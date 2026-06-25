using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Notification;

public record GetNotificationsQuery : IRequest<Result<IReadOnlyList<NotificationDto>>>;

public sealed class GetNotificationsQueryHandler(INotificationRepository repository)
    : IRequestHandler<GetNotificationsQuery, Result<IReadOnlyList<NotificationDto>>>
{
    public async Task<Result<IReadOnlyList<NotificationDto>>> Handle(
        GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var notifications = await repository.GetAllAsync(cancellationToken);
        var dtos = notifications
            .Select(n => new NotificationDto(
                n.NotificationId,
                n.Title,
                n.Message,
                TypeLabel(n.Type),
                n.DeepLinkUrl,
                n.IsRead,
                n.Timestamp))
            .ToList();
        return Result<IReadOnlyList<NotificationDto>>.Success(dtos);
    }

    private static string TypeLabel(Domain.Notification.NotificationType type) => type switch
    {
        Domain.Notification.NotificationType.Quest       => "QUEST",
        Domain.Notification.NotificationType.Warning     => "WARNING",
        Domain.Notification.NotificationType.ShieldUpdate => "SHIELD",
        Domain.Notification.NotificationType.WeeklyRecap  => "RECAP",
        _                                                 => "INFO"
    };
}
