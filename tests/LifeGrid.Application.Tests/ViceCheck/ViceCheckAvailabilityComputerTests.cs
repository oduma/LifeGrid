using FluentAssertions;
using LifeGrid.Application.ViceCheck;

namespace LifeGrid.Application.Tests.ViceCheck;

public sealed class ViceCheckAvailabilityComputerTests
{
    private static readonly DateTime WeekStartDate = new(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc); // Monday
    private static readonly DateTime EndDate       = WeekStartDate.AddDays(6);                    // Sunday 00:00

    [Fact]
    public void WithinWindow_ReturnsTrue()
    {
        var now = EndDate.AddHours(10);

        var result = ViceCheckAvailabilityComputer.IsVisible(true, true, WeekStartDate, now, false);

        result.Should().BeTrue();
    }

    [Fact]
    public void AtExactCutoff_ReturnsTrue()
    {
        var now = EndDate.AddHours(72);

        var result = ViceCheckAvailabilityComputer.IsVisible(true, true, WeekStartDate, now, false);

        result.Should().BeTrue();
    }

    [Fact]
    public void _73HoursPastEndDate_ReturnsFalse()
    {
        var now = EndDate.AddHours(73);

        var result = ViceCheckAvailabilityComputer.IsVisible(true, true, WeekStartDate, now, false);

        result.Should().BeFalse();
    }

    [Fact]
    public void SurveyNotCompleted_ReturnsFalse()
    {
        var now = EndDate.AddHours(10);

        var result = ViceCheckAvailabilityComputer.IsVisible(false, true, WeekStartDate, now, false);

        result.Should().BeFalse();
    }

    [Fact]
    public void WeekNotClosed_ReturnsFalse()
    {
        var now = EndDate.AddHours(10);

        var result = ViceCheckAvailabilityComputer.IsVisible(true, false, WeekStartDate, now, false);

        result.Should().BeFalse();
    }

    [Fact]
    public void AlreadyAudited_ReturnsFalse()
    {
        var now = EndDate.AddHours(10);

        var result = ViceCheckAvailabilityComputer.IsVisible(true, true, WeekStartDate, now, true);

        result.Should().BeFalse();
    }
}
