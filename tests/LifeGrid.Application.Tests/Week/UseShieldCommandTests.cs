using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Gamification;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Week;
using LifeGrid.Domain.WeekGoal;
using NSubstitute;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;
using WeekGoalEntity    = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.Tests.Week;

public sealed class UseShieldCommandTests
{
    private readonly IWeekRepository          _weekRepo    = Substitute.For<IWeekRepository>();
    private readonly IUserProfileRepository   _profileRepo = Substitute.For<IUserProfileRepository>();
    private readonly IUnitOfWork              _uow         = Substitute.For<IUnitOfWork>();
    private readonly IEconomyStateBroadcaster _broadcaster = Substitute.For<IEconomyStateBroadcaster>();
    private readonly UseShieldCommandHandler  _handler;

    public UseShieldCommandTests()
    {
        _handler = new UseShieldCommandHandler(_weekRepo, _profileRepo, _uow, _broadcaster);
    }

    [Fact]
    public async Task Handle_Level1Warning_ConsumesShield_ClearsState()
    {
        var weekGoal = WeekGoalEntity.Create(Guid.NewGuid(), Guid.NewGuid(), 1);
        weekGoal.SetPenaltyState(PenaltyState.Level1Warning);

        var profile = UserProfileEntity.Create();
        profile.GrantBonusShield();

        _weekRepo.GetWeekGoalByIdAsync(weekGoal.WeekGoalId, Arg.Any<CancellationToken>())
                 .Returns(weekGoal);
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>())
                    .Returns(profile);

        var result = await _handler.Handle(new UseShieldCommand(weekGoal.WeekGoalId), default);

        result.IsSuccess.Should().BeTrue();
        weekGoal.PenaltyState.Should().Be(PenaltyState.Clean);
        profile.Economy.ShieldsAvailable.Should().Be(0);
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        _broadcaster.Received(1).Broadcast();
    }

    [Fact]
    public async Task Handle_NoShieldsAvailable_ReturnsFailure()
    {
        var weekGoal = WeekGoalEntity.Create(Guid.NewGuid(), Guid.NewGuid(), 1);
        weekGoal.SetPenaltyState(PenaltyState.Level1Warning);

        var profile = UserProfileEntity.Create(); // 0 shields

        _weekRepo.GetWeekGoalByIdAsync(weekGoal.WeekGoalId, Arg.Any<CancellationToken>())
                 .Returns(weekGoal);
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>())
                    .Returns(profile);

        var result = await _handler.Handle(new UseShieldCommand(weekGoal.WeekGoalId), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("no_shields");
        weekGoal.PenaltyState.Should().Be(PenaltyState.Level1Warning);
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StateNotLevel1Warning_ReturnsFailure()
    {
        var weekGoal = WeekGoalEntity.Create(Guid.NewGuid(), Guid.NewGuid(), 1);
        weekGoal.SetPenaltyState(PenaltyState.ProbationWeek2);

        _weekRepo.GetWeekGoalByIdAsync(weekGoal.WeekGoalId, Arg.Any<CancellationToken>())
                 .Returns(weekGoal);

        var result = await _handler.Handle(new UseShieldCommand(weekGoal.WeekGoalId), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("invalid_state");
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WeekGoalNotFound_ReturnsFailure()
    {
        _weekRepo.GetWeekGoalByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                 .Returns((WeekGoalEntity?)null);

        var result = await _handler.Handle(new UseShieldCommand(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("weekgoal_not_found");
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
