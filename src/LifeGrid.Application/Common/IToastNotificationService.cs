using LifeGrid.Application.Badge;

namespace LifeGrid.Application.Common;

public interface IToastNotificationService
{
    Task ShowBadgesEarnedAsync(IReadOnlyCollection<BadgeDto> badges, CancellationToken ct = default);
}
