using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Week;
using NSubstitute;
using WeekEntity = LifeGrid.Domain.Week.Week;

namespace LifeGrid.Application.Tests.Week;

public sealed class CloseWeekCommandTests
{
    private readonly IWeekRepository          _weekRepo    = Substitute.For<IWeekRepository>();
    private readonly IUnitOfWork              _uow         = Substitute.For<IUnitOfWork>();
    private readonly IEconomyStateBroadcaster _broadcaster = Substitute.For<IEconomyStateBroadcaster>();
    private readonly CloseWeekCommandHandler  _handler;

    private static readonly DateTime Monday = new(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc);

    public CloseWeekCommandTests()
    {
        _handler = new CloseWeekCommandHandler(_weekRepo, _uow, _broadcaster);
    }

    [Fact]
    public async Task Handle_ExistingWeek_SetsStatusToClosedAndCommits()
    {
        var week = WeekEntity.Create(5, Monday);
        _weekRepo.GetByIdAsync(week.WeekId, Arg.Any<CancellationToken>()).Returns(week);

        var result = await _handler.Handle(new CloseWeekCommand(week.WeekId), default);

        result.IsSuccess.Should().BeTrue();
        week.Status.Should().Be(WeekStatus.Closed);
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        _broadcaster.Received(1).Broadcast();
    }

    [Fact]
    public async Task Handle_WeekNotFound_ReturnsFailure()
    {
        _weekRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                 .Returns((WeekEntity?)null);

        var result = await _handler.Handle(new CloseWeekCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("week_not_found");
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
