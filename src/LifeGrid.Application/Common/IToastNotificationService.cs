using LifeGrid.Application.Badge;

namespace LifeGrid.Application.Common;

public interface IToastNotificationService
{
    Task ShowBadgesEarnedAsync(IReadOnlyCollection<BadgeDto> badges, CancellationToken ct = default);
    Task ShowInfoAsync(string title, string message, CancellationToken ct = default);
}
