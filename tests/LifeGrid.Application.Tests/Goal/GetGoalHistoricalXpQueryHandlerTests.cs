using FluentAssertions;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Week;
using NSubstitute;

namespace LifeGrid.Application.Tests.Goal;

public sealed class GetGoalHistoricalXpQueryHandlerTests
{
    private readonly IWeekRepository              _weekRepo = Substitute.For<IWeekRepository>();
    private readonly GetGoalHistoricalXpQueryHandler _handler;

    public GetGoalHistoricalXpQueryHandlerTests()
        => _handler = new GetGoalHistoricalXpQueryHandler(_weekRepo);

    [Fact]
    public async Task NoWeekGoals_ReturnsZero()
    {
        var goalId = Guid.NewGuid();
        _weekRepo.GetHistoricalXpByGoalIdAsync(goalId, Arg.Any<CancellationToken>()).Returns(0);

        var result = await _handler.Handle(new GetGoalHistoricalXpQuery(goalId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    [Fact]
    public async Task WithWeekGoals_ReturnsCorrectSum()
    {
        var goalId = Guid.NewGuid();
        // Two WeekGoals: 200 + 250 = 450 XP total
        _weekRepo.GetHistoricalXpByGoalIdAsync(goalId, Arg.Any<CancellationToken>()).Returns(450);

        var result = await _handler.Handle(new GetGoalHistoricalXpQuery(goalId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(450);
    }
}
