namespace LifeGrid.Application.Notification;

public static class NotificationRouteParser
{
    public static string? ToShellRoute(string? deepLinkUrl)
    {
        if (deepLinkUrl is null) return null;
        if (!Uri.TryCreate(deepLinkUrl, UriKind.Absolute, out var uri)) return null;
        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return uri.Host switch
        {
            "habit"   when segments.Length > 0 => $"habit-logging?HabitId={segments[0]}",
            "goal"    when segments.Length > 0 => "goals",
            "week"    when segments.Length > 0 => $"week-detail?weekId={segments[0]}",
            "summary" when segments.Length > 0 => $"week-summary?weekId={segments[0]}",
            _ => null
        };
    }
}
