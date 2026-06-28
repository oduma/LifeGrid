using LifeGrid.Application.Common;
using Plugin.LocalNotification;

namespace LifeGrid.Presentation.Services;

internal sealed class LocalPushNotificationService : IPushNotificationService
{
    private static int _nextId = 2000;

    public Task SendAsync(string title, string body, string? deepLinkUrl = null,
                          CancellationToken ct = default)
    {
        LocalNotificationCenter.Current.Show(new NotificationRequest
        {
            NotificationId = System.Threading.Interlocked.Increment(ref _nextId),
            Title          = title,
            Description    = body,
            ReturningData  = deepLinkUrl ?? string.Empty
        });
        return Task.CompletedTask;
    }

    public Task ScheduleAsync(string title, string body, DateTime fireAtLocal,
                              string? deepLinkUrl = null, CancellationToken ct = default)
    {
        LocalNotificationCenter.Current.Show(new NotificationRequest
        {
            NotificationId = System.Threading.Interlocked.Increment(ref _nextId),
            Title          = title,
            Description    = body,
            ReturningData  = deepLinkUrl ?? string.Empty,
            Schedule       = new NotificationRequestSchedule
            {
                NotifyTime = fireAtLocal
            }
        });
        return Task.CompletedTask;
    }
}
