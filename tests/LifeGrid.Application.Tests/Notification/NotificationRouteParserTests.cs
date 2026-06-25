using FluentAssertions;
using LifeGrid.Application.Notification;

namespace LifeGrid.Application.Tests.Notification;

public sealed class NotificationRouteParserTests
{
    [Fact]
    public void ToShellRoute_HabitDeepLink_ReturnsHabitLoggingRoute()
    {
        var habitId = Guid.NewGuid();
        var url     = $"lifegrid://habit/{habitId}";

        var route = NotificationRouteParser.ToShellRoute(url);

        route.Should().Be($"habit-logging?HabitId={habitId}");
    }

    [Fact]
    public void ToShellRoute_GoalDeepLink_ReturnsGoalsRoute()
    {
        var goalId = Guid.NewGuid();
        var url    = $"lifegrid://goal/{goalId}";

        var route = NotificationRouteParser.ToShellRoute(url);

        route.Should().Be("goals");
    }

    [Fact]
    public void ToShellRoute_NullUrl_ReturnsNull()
    {
        var route = NotificationRouteParser.ToShellRoute(null);

        route.Should().BeNull();
    }

    [Fact]
    public void ToShellRoute_InvalidUri_ReturnsNull()
    {
        var route = NotificationRouteParser.ToShellRoute("not-a-uri");

        route.Should().BeNull();
    }

    [Fact]
    public void ToShellRoute_UnknownSchemeHost_ReturnsNull()
    {
        var route = NotificationRouteParser.ToShellRoute("lifegrid://unknown/some-id");

        route.Should().BeNull();
    }
}
