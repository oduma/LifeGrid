using LifeGrid.Application.Common;
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Notification;

public record MarkNotificationReadCommand(Guid NotificationId) : IRequest<Result>;

public sealed class MarkNotificationReadCommandHandler(
    INotificationRepository notificationRepository,
    IUnitOfWork             unitOfWork)
    : IRequestHandler<MarkNotificationReadCommand, Result>
{
    public async Task<Result> Handle(
        MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await notificationRepository.GetByIdAsync(
            request.NotificationId, cancellationToken);
        if (notification is null)
            return Result.Failure("notification_not_found");

        notification.MarkRead();
        await unitOfWork.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
