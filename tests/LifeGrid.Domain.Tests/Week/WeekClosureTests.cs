using FluentAssertions;
using LifeGrid.Domain.Week;
using WeekEntity = LifeGrid.Domain.Week.Week;

namespace LifeGrid.Domain.Tests.Week;

public sealed class WeekClosureTests
{
    private static readonly DateTime Monday = new(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Close_SetsStatusToClosed()
    {
        var week = WeekEntity.Create(5, Monday);
        week.Status.Should().Be(WeekStatus.Active);

        week.Close();

        week.Status.Should().Be(WeekStatus.Closed);
    }

    [Fact]
    public void Close_CalledTwice_StatusRemainsClosedWithoutException()
    {
        var week = WeekEntity.Create(5, Monday);
        week.Close();

        var act = () => week.Close();

        act.Should().NotThrow();
        week.Status.Should().Be(WeekStatus.Closed);
    }
}
