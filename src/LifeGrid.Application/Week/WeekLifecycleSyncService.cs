using LifeGrid.Application.Common;
using LifeGrid.Application.Notification;
using LifeGrid.Domain.Week;
using MediatR;
using NotificationEntity = LifeGrid.Domain.Notification.Notification;
using NotificationType   = LifeGrid.Domain.Notification.NotificationType;

namespace LifeGrid.Application.Week;

public sealed class WeekLifecycleSyncService(
    IWeekRepository         weekRepository,
    INotificationRepository notificationRepository,
    IUnitOfWork             unitOfWork,
    IDateTimeProvider       dateTimeProvider,
    ISender                 sender,
    IPushNotificationService pushNotificationService)
    : IWeekLifecycleSyncService
{
    public async Task EvaluateAsync(CancellationToken ct = default)
    {
        var today          = dateTimeProvider.UtcNow.Date;
        var previousMonday = GetPreviousWeekMonday(today);

        var week = await weekRepository.GetByStartDateAsync(previousMonday, ct);
        if (week is null || week.Status == WeekStatus.Closed)
            return;

        if (today.DayOfWeek == DayOfWeek.Wednesday)
            await HandleWednesdayAsync(week, ct);
        else if (today.DayOfWeek == DayOfWeek.Monday)
            await HandleMondayAsync(week, ct);
    }

    private async Task HandleWednesdayAsync(Domain.Week.Week week, CancellationToken ct)
    {
        await sender.Send(new CloseWeekCommand(week.WeekId), ct);

        var notification = NotificationEntity.Create(
            "Week Auto-Closed",
            "Your previous week was automatically closed by the system.",
            NotificationType.Warning,
            $"lifegrid://summary/{week.WeekId}",
            dateTimeProvider.UtcNow);
        await notificationRepository.AddAsync(notification, ct);
        await unitOfWork.CommitAsync(ct);

        await pushNotificationService.SendAsync(
            "Week Auto-Closed",
            "Your previous week was automatically closed by the system.",
            $"lifegrid://summary/{week.WeekId}",
            ct);
    }

    private async Task HandleMondayAsync(Domain.Week.Week week, CancellationToken ct)
    {
        var notification = NotificationEntity.Create(
            "Week Ended",
            "Please review and close your previous week.",
            NotificationType.Warning,
            $"lifegrid://week/{week.WeekId}",
            dateTimeProvider.UtcNow);
        await notificationRepository.AddAsync(notification, ct);
        await unitOfWork.CommitAsync(ct);

        await pushNotificationService.SendAsync(
            "Week Ended",
            "Please review and close your previous week.",
            $"lifegrid://week/{week.WeekId}",
            ct);
    }

    public static DateTime GetPreviousWeekMonday(DateTime today)
    {
        int daysToLastSunday = today.DayOfWeek == DayOfWeek.Sunday
            ? 0 : (int)today.DayOfWeek;
        return today.AddDays(-daysToLastSunday - 6);
    }
}
