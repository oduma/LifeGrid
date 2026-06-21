using FluentAssertions;
using LifeGrid.Domain.Goal;
using GoalAggregate = LifeGrid.Domain.Goal.Goal;

namespace LifeGrid.Domain.Tests.Goal;

public sealed class GoalRefinementAnswerTests
{
    private static GoalAggregate BuildGoal() =>
        GoalAggregate.Create(
            userId:       Guid.NewGuid(),
            description:  "Run a marathon",
            ambientTag:   "Physical",
            duration:     "6 months",
            deadlineDate: new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            creationDate: DateTime.Now);

    [Fact]
    public void SetRefinementAnswers_CreatesOwnedCollection()
    {
        var goal  = BuildGoal();
        var items = new (int, string, string?)[]
        {
            (1, "What is your age and gender?",           "32, male"),
            (2, "What is your current running baseline?", "5k comfortable"),
            (3, "Any physical limitations?",              null)
        };

        goal.SetRefinementAnswers(items);

        goal.RefinementAnswers.Should().HaveCount(3);
    }

    [Fact]
    public void SetRefinementAnswers_SetsQuestionAndAnswer()
    {
        var goal  = BuildGoal();
        goal.SetRefinementAnswers(new (int, string, string?)[] { (1, "What is your age?", "30") });

        var answer = goal.RefinementAnswers.Single();
        answer.RankOrder.Should().Be(1);
        answer.Question.Should().Be("What is your age?");
        answer.Answer.Should().Be("30");
    }

    [Fact]
    public void SetRefinementAnswers_AllowsNullAnswer()
    {
        var goal = BuildGoal();
        goal.SetRefinementAnswers(new[] { (1, "Question?", (string?)null) });

        goal.RefinementAnswers.Single().Answer.Should().BeNull();
    }

    [Fact]
    public void SetRefinementAnswers_AssignsNonEmptyGuidsToEachItem()
    {
        var goal = BuildGoal();
        goal.SetRefinementAnswers(new (int, string, string?)[] { (1, "Q1", "A1"), (2, "Q2", "A2") });

        var ids = goal.RefinementAnswers.Select(r => r.RefinementAnswerId).ToList();
        ids.Should().OnlyContain(id => id != Guid.Empty);
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void SetRefinementAnswers_ReplacesExistingAnswers()
    {
        var goal = BuildGoal();
        goal.SetRefinementAnswers(new (int, string, string?)[] { (1, "Old question", "old answer") });
        goal.SetRefinementAnswers(new (int, string, string?)[] { (1, "New question", "new answer") });

        goal.RefinementAnswers.Should().HaveCount(1);
        goal.RefinementAnswers.Single().Question.Should().Be("New question");
    }

    [Fact]
    public void Create_HasEmptyRefinementAnswersCollection()
    {
        var goal = BuildGoal();
        goal.RefinementAnswers.Should().BeEmpty();
    }
}
