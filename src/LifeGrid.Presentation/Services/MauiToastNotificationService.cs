using LifeGrid.Application.Badge;
using LifeGrid.Application.Common;
using MauiApplication = Microsoft.Maui.Controls.Application;

namespace LifeGrid.Presentation.Services;

internal sealed class MauiToastNotificationService : IToastNotificationService
{
    public async Task ShowBadgesEarnedAsync(
        IReadOnlyCollection<BadgeDto> badges, CancellationToken ct = default)
    {
        foreach (var badge in badges)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                MauiApplication.Current!.MainPage!.DisplayAlert(
                    "Badge Unlocked!",
                    $"{badge.BadgeName} — {badge.Description}",
                    "Nice!"));
        }
    }
}
