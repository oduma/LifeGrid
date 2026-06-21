using FluentAssertions;
using LifeGrid.Domain.Goal;
using GoalAggregate = LifeGrid.Domain.Goal.Goal;

namespace LifeGrid.Domain.Tests.Goal;

public sealed class GoalDomainMutationTests
{
    // 2026-01-05 is a Monday → StartDate = same date (no offset needed)
    private static readonly DateTime MondayStart = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

    private static GoalAggregate BuildGoal(int durationDays = 100)
    {
        var deadline = MondayStart.AddDays(durationDays);
        return GoalAggregate.Create(
            userId:       Guid.NewGuid(),
            description:  "Test goal",
            ambientTag:   "Test",
            duration:     $"{durationDays} days",
            deadlineDate: deadline,
            creationDate: MondayStart);
    }

    [Fact]
    public void MarkAbandoned_SetsStatusToAbandoned()
    {
        var goal = BuildGoal();

        goal.MarkAbandoned();

        goal.Status.Should().Be(GoalStatus.Abandoned);
    }

    [Fact]
    public void ExtendDeadlineByPercent_100DayGoal_AddsExactly25Days()
    {
        var goal             = BuildGoal(durationDays: 100);
        var originalDeadline = goal.DeadlineDate;

        goal.ExtendDeadlineByPercent(25.0);

        goal.DeadlineDate.Should().Be(originalDeadline.AddDays(25));
    }

    [Fact]
    public void ExtendDeadlineByPercent_UpdatesDurationString()
    {
        var goal = BuildGoal(durationDays: 100);

        goal.ExtendDeadlineByPercent(25.0);

        goal.Duration.Should().NotBeNullOrEmpty();
        goal.Duration.Should().NotBe("100 days");
    }

    [Fact]
    public void ExtendDeadlineByPercent_DoesNotChangeStartDate()
    {
        var goal          = BuildGoal(durationDays: 100);
        var originalStart = goal.StartDate;

        goal.ExtendDeadlineByPercent(25.0);

        goal.StartDate.Should().Be(originalStart);
    }

    [Fact]
    public void ExtendDeadlineByPercent_ShortGoal_UseWeeksInDurationString()
    {
        var goal = BuildGoal(durationDays: 14); // 14 days → 17.5 → 18 days total

        goal.ExtendDeadlineByPercent(25.0);

        goal.Duration.Should().Contain("week");
    }

    [Fact]
    public void ExtendDeadlineByPercent_LongGoal_UseMonthsInDurationString()
    {
        var goal = BuildGoal(durationDays: 180); // 180 days → 225 days total

        goal.ExtendDeadlineByPercent(25.0);

        goal.Duration.Should().Contain("month");
    }
}
