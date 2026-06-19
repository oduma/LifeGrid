using FluentAssertions;
using GoalAggregate = LifeGrid.Domain.Goal.Goal;

namespace LifeGrid.Domain.Tests.Vice;

public sealed class GoalLinkedBadHabitsTests
{
    private static GoalAggregate BuildGoal() =>
        GoalAggregate.Create(
            userId:       Guid.NewGuid(),
            description:  "Run a marathon",
            ambientTag:   "Fitness",
            duration:     "6 months",
            deadlineDate: DateTime.UtcNow.AddMonths(6));

    [Fact]
    public void SetLinkedBadHabits_PopulatesCollection()
    {
        var goal = BuildGoal();
        goal.SetLinkedBadHabits(new[]
        {
            ("Late-night scrolling", 3),
            ("Sugar cravings",       5)
        });
        goal.LinkedBadHabits.Should().HaveCount(2);
    }

    [Fact]
    public void SetLinkedBadHabits_StoresDescriptionAndDangerLevel()
    {
        var goal = BuildGoal();
        goal.SetLinkedBadHabits(new[] { ("Doomscrolling", 4) });

        var habit = goal.LinkedBadHabits.Single();
        habit.Description.Should().Be("Doomscrolling");
        habit.DangerLevel.Should().Be(4);
    }

    [Fact]
    public void SetLinkedBadHabits_AssignsNonEmptyBadHabitId()
    {
        var goal = BuildGoal();
        goal.SetLinkedBadHabits(new[] { ("Snacking", 2) });
        goal.LinkedBadHabits.Single().BadHabitId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void SetLinkedBadHabits_ReplacesExistingCollection()
    {
        var goal = BuildGoal();
        goal.SetLinkedBadHabits(new[] { ("Old vice", 1) });
        goal.SetLinkedBadHabits(new[] { ("New vice A", 2), ("New vice B", 3) });
        goal.LinkedBadHabits.Should().HaveCount(2);
        goal.LinkedBadHabits.Should().NotContain(h => h.Description == "Old vice");
    }

    [Fact]
    public void SetLinkedBadHabits_EmptyList_ClearsCollection()
    {
        var goal = BuildGoal();
        goal.SetLinkedBadHabits(new[] { ("Some vice", 1) });
        goal.SetLinkedBadHabits(Array.Empty<(string, int)>());
        goal.LinkedBadHabits.Should().BeEmpty();
    }
}
