using FluentAssertions;
using LifeGrid.Application.Week;

namespace LifeGrid.Application.Tests.Week;

public sealed class WeekClosureStateComputerTests
{
    private static readonly DateTime WeekStart = new(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
    // week ends June 21 (StartDate + 6 days)

    [Fact]
    public void Compute_ActivePastWeek_ShowsCloseButton_EnablesLogging()
    {
        var today = new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc); // past end date

        var (isLogging, isClose, isSummary) =
            WeekClosureStateComputer.Compute("Active", WeekStart, today);

        isLogging.Should().BeTrue();
        isClose.Should().BeTrue();
        isSummary.Should().BeFalse();
    }

    [Fact]
    public void Compute_ClosedWeek_ShowsSummaryButton_DisablesLogging()
    {
        var today = new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc);

        var (isLogging, isClose, isSummary) =
            WeekClosureStateComputer.Compute("Closed", WeekStart, today);

        isLogging.Should().BeFalse();
        isClose.Should().BeFalse();
        isSummary.Should().BeTrue();
    }

    [Fact]
    public void Compute_ActiveCurrentWeek_NoButtonsVisible_LoggingEnabled()
    {
        // Current week starts June 22, ends June 28. Today = June 25 (within week).
        var currentWeekStart = new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc);
        var today            = new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc);

        var (isLogging, isClose, isSummary) =
            WeekClosureStateComputer.Compute("Active", currentWeekStart, today);

        isLogging.Should().BeTrue();
        isClose.Should().BeFalse(); // endDate June 28 is NOT < June 25
        isSummary.Should().BeFalse();
    }
}
