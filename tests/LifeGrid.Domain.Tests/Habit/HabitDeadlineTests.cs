using FluentAssertions;
using HabitEntity = LifeGrid.Domain.Habit.Habit;
using LifeGrid.Domain.Habit;

namespace LifeGrid.Domain.Tests.Habit;

public sealed class HabitDeadlineTests
{
    private static HabitEntity BuildHabit(DateTime deadline) => HabitEntity.Create(
        Guid.NewGuid(), HabitType.Flash, "Quest", "Description", 1.0, "reps", deadline);

    [Fact]
    public void IsBeforeDeadline_BeforeDeadline_ReturnsTrue()
    {
        var deadline = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);
        var habit    = BuildHabit(deadline);

        habit.IsBeforeDeadline(deadline.AddMinutes(-1)).Should().BeTrue();
    }

    [Fact]
    public void IsBeforeDeadline_ExactlyAtDeadline_ReturnsTrue()
    {
        var deadline = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);
        var habit    = BuildHabit(deadline);

        habit.IsBeforeDeadline(deadline).Should().BeTrue();
    }

    [Fact]
    public void IsBeforeDeadline_AfterDeadline_ReturnsFalse()
    {
        var deadline = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);
        var habit    = BuildHabit(deadline);

        habit.IsBeforeDeadline(deadline.AddMinutes(1)).Should().BeFalse();
    }
}
