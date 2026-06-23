using FluentAssertions;
using WeekEntity     = LifeGrid.Domain.Week.Week;
using WeekGoalEntity = LifeGrid.Domain.WeekGoal.WeekGoal;
using HabitEntity    = LifeGrid.Domain.Habit.Habit;
using LifeGrid.Domain.Habit;
using LifeGrid.Domain.Week;
using LifeGrid.Domain.WeekGoal;

namespace LifeGrid.Domain.Tests.Habit;

public sealed class HabitEntityTests
{
    // ── Week ────────────────────────────────────────────────────────────────

    [Fact]
    public void Week_Create_SetsStatusToActive()
    {
        var week = WeekEntity.Create(1, new DateTime(2026, 6, 16));

        week.Status.Should().Be(WeekStatus.Active);
    }

    [Fact]
    public void Week_Create_SetsWeekNumberAndStartDate()
    {
        var start = new DateTime(2026, 6, 16);
        var week  = WeekEntity.Create(3, start);

        week.WeekNumber.Should().Be(3);
        week.StartDate.Should().Be(start);
    }

    [Fact]
    public void Week_Create_SetsDefaultSpEarnedToZero()
    {
        var week = WeekEntity.Create(1, DateTime.UtcNow);

        week.TotalWeeklySpEarned.Should().Be(0);
    }

    [Fact]
    public void Week_Create_HasEmptyWeekGoalCollection()
    {
        var week = WeekEntity.Create(1, DateTime.UtcNow);

        week.WeekGoals.Should().BeEmpty();
    }

    [Fact]
    public void Week_Create_GeneratesNonEmptyId()
    {
        var week = WeekEntity.Create(1, DateTime.UtcNow);

        week.WeekId.Should().NotBe(Guid.Empty);
    }

    // ── WeekGoal ────────────────────────────────────────────────────────────

    [Fact]
    public void WeekGoal_Create_SetsPenaltyStateToClean()
    {
        var weekGoal = WeekGoalEntity.Create(Guid.NewGuid(), Guid.NewGuid(), 1);

        weekGoal.PenaltyState.Should().Be(PenaltyState.Clean);
    }

    [Fact]
    public void WeekGoal_Create_SetsDefaultGpAndXpToZero()
    {
        var weekGoal = WeekGoalEntity.Create(Guid.NewGuid(), Guid.NewGuid(), 1);

        weekGoal.GoalWeeklyGp.Should().Be(0.0);
        weekGoal.GoalWeeklyXpEarned.Should().Be(0);
    }

    [Fact]
    public void WeekGoal_Create_PreservesWeekIdAndGoalId()
    {
        var weekId = Guid.NewGuid();
        var goalId = Guid.NewGuid();

        var weekGoal = WeekGoalEntity.Create(weekId, goalId, 1);

        weekGoal.WeekId.Should().Be(weekId);
        weekGoal.GoalId.Should().Be(goalId);
    }

    // ── Habit ───────────────────────────────────────────────────────────────

    [Fact]
    public void Habit_Create_SetsHabitTypeToPlanned()
    {
        var habit = HabitEntity.Create(
            Guid.NewGuid(), HabitType.Planned, "Run", "Go for a run", 5.0, "km",
            DateTime.UtcNow.AddDays(7));

        habit.HabitType.Should().Be(HabitType.Planned);
    }

    [Fact]
    public void Habit_Create_PreservesAllTargetFields()
    {
        var weekGoalId = Guid.NewGuid();
        var deadline   = new DateTime(2026, 6, 22);

        var habit = HabitEntity.Create(
            weekGoalId, HabitType.Planned, "Run 5k", "Run 5 kilometres", 5.0, "km", deadline);

        habit.WeekGoalId.Should().Be(weekGoalId);
        habit.HabitName.Should().Be("Run 5k");
        habit.HabitDescription.Should().Be("Run 5 kilometres");
        habit.TargetValue.Should().Be(5.0);
        habit.MeasurementUnit.Should().Be("km");
        habit.DeadlineDateTime.Should().Be(deadline);
    }

    [Fact]
    public void Habit_Create_GeneratesNonEmptyId()
    {
        var habit = HabitEntity.Create(
            Guid.NewGuid(), HabitType.Planned, "Run", "Desc", 5.0, "km", DateTime.UtcNow);

        habit.HabitId.Should().NotBe(Guid.Empty);
    }
}
