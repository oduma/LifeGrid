using FluentAssertions;
using WeekGoalEntity = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Domain.Tests.WeekGoal;

public sealed class WeekGoalRetroactivePenaltyTests
{
    [Fact]
    public void ApplyRetroactiveGpPenalty_SetsNewGp()
    {
        var weekGoal = WeekGoalEntity.Create(Guid.NewGuid(), Guid.NewGuid(), 1);
        weekGoal.RecordMetricsUpdate(82.0, 0);

        weekGoal.ApplyRetroactiveGpPenalty(77.0);

        weekGoal.GoalWeeklyGp.Should().Be(77.0);
    }

    [Fact]
    public void ApplyRetroactiveGpPenalty_ClampsAtZero()
    {
        var weekGoal = WeekGoalEntity.Create(Guid.NewGuid(), Guid.NewGuid(), 1);
        weekGoal.RecordMetricsUpdate(3.0, 0);

        weekGoal.ApplyRetroactiveGpPenalty(-5.0);

        weekGoal.GoalWeeklyGp.Should().Be(0.0);
    }
}
