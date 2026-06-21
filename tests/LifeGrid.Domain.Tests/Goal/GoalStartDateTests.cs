using FluentAssertions;
using GoalAggregate = LifeGrid.Domain.Goal.Goal;

namespace LifeGrid.Domain.Tests.Goal;

public sealed class GoalStartDateTests
{
    // ── CalculateStartDate formula ────────────────────────────────────────────
    // 2026-06-15 = Monday  (verified: Jan 1 2026 = Thursday; +165 days = Monday)
    // 2026-06-14 = Sunday
    // 2026-06-16 = Tuesday  ...  2026-06-21 = Sunday  →  next Monday = 2026-06-22

    [Theory]
    [InlineData(2026, 6, 14, 2026, 6, 15)] // Sunday    → next Monday (2026-06-15)
    [InlineData(2026, 6, 15, 2026, 6, 15)] // Monday    → same Monday
    [InlineData(2026, 6, 16, 2026, 6, 22)] // Tuesday   → next Monday (2026-06-22)
    [InlineData(2026, 6, 17, 2026, 6, 22)] // Wednesday → next Monday
    [InlineData(2026, 6, 18, 2026, 6, 22)] // Thursday  → next Monday
    [InlineData(2026, 6, 19, 2026, 6, 22)] // Friday    → next Monday
    [InlineData(2026, 6, 20, 2026, 6, 22)] // Saturday  → next Monday
    public void CalculateStartDate_AllDaysOfWeek_ReturnCorrectMonday(
        int createdYear, int createdMonth, int createdDay,
        int expectedYear, int expectedMonth, int expectedDay)
    {
        var creationDate = new DateTime(createdYear, createdMonth, createdDay);
        var expected     = new DateTime(expectedYear, expectedMonth, expectedDay);

        var result = GoalAggregate.CalculateStartDate(creationDate);

        result.Should().Be(expected);
        result.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public void CalculateStartDate_CreatedOnMonday_ReturnsSameDay()
    {
        var monday = new DateTime(2026, 6, 15); // 2026-06-15 is a Monday

        GoalAggregate.CalculateStartDate(monday).Should().Be(monday);
    }

    [Fact]
    public void CalculateStartDate_ResultIsAlwaysMidnight()
    {
        var creationDate = new DateTime(2026, 6, 17, 14, 30, 59); // Wednesday with time component

        var result = GoalAggregate.CalculateStartDate(creationDate);

        result.TimeOfDay.Should().Be(TimeSpan.Zero);
    }

    // ── Goal.Create stores StartDate ──────────────────────────────────────────

    [Fact]
    public void Create_SetsStartDateToComputedMonday()
    {
        var thursday     = new DateTime(2026, 6, 18); // Thursday → next Monday is 2026-06-22
        var expectedDate = new DateTime(2026, 6, 22);

        var goal = GoalAggregate.Create(
            Guid.NewGuid(), "Run a marathon", "Fitness", "6 months",
            new DateTime(2027, 1, 1), thursday);

        goal.StartDate.Should().Be(expectedDate);
    }

    [Fact]
    public void Create_WhenCreatedOnMonday_StartDateEqualsCreationDate()
    {
        var monday = new DateTime(2026, 6, 15); // 2026-06-15 is a Monday

        var goal = GoalAggregate.Create(
            Guid.NewGuid(), "Run a marathon", "Fitness", "6 months",
            new DateTime(2027, 1, 1), monday);

        goal.StartDate.Should().Be(monday);
    }

    [Fact]
    public void Create_StartDateDayOfWeek_IsAlwaysMonday()
    {
        var friday = new DateTime(2026, 6, 19); // Friday

        var goal = GoalAggregate.Create(
            Guid.NewGuid(), "Run a marathon", "Fitness", "6 months",
            new DateTime(2027, 1, 1), friday);

        goal.StartDate.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }
}
