using FluentAssertions;
using GoalAggregate = LifeGrid.Domain.Goal.Goal;
using LifeGrid.Domain.Goal;

namespace LifeGrid.Domain.Tests.Goal;

public sealed class GoalTests
{
    private static GoalAggregate BuildGoal() =>
        GoalAggregate.Create(
            userId:       Guid.NewGuid(),
            description:  "Run a marathon",
            ambientTag:   "Fitness",
            duration:     "6 months",
            deadlineDate: DateTime.UtcNow.AddMonths(6),
            creationDate: DateTime.Now);

    [Fact]
    public void Create_GeneratesNonEmptyGoalId()
    {
        var goal = BuildGoal();
        goal.GoalId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_SetsStatusToActive()
    {
        var goal = BuildGoal();
        goal.Status.Should().Be(GoalStatus.Active);
    }

    [Fact]
    public void Create_HasEmptyLinkedBadHabitsCollection()
    {
        var goal = BuildGoal();
        goal.LinkedBadHabits.Should().BeEmpty();
    }
}
