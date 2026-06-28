namespace LifeGrid.Application.Common;

public interface IPushNotificationService
{
    Task SendAsync(string title, string body, string? deepLinkUrl = null,
                   CancellationToken ct = default);
    Task ScheduleAsync(string title, string body, DateTime fireAtLocal,
                       string? deepLinkUrl = null, CancellationToken ct = default);
}
