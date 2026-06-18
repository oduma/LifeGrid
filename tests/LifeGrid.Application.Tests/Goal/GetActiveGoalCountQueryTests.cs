using FluentAssertions;
using LifeGrid.Application.Goal;
using LifeGrid.Application.UserProfile;
using NSubstitute;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Application.Tests.Goal;

public sealed class GetActiveGoalCountQueryTests
{
    private readonly IUserProfileRepository _profileRepo = Substitute.For<IUserProfileRepository>();
    private readonly IGoalRepository        _goalRepo    = Substitute.For<IGoalRepository>();
    private readonly GetActiveGoalCountQueryHandler _handler;

    public GetActiveGoalCountQueryTests()
        => _handler = new GetActiveGoalCountQueryHandler(_profileRepo, _goalRepo);

    [Fact]
    public async Task NoProfile_ReturnsZero()
    {
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns((UserProfileEntity?)null);

        var result = await _handler.Handle(new GetActiveGoalCountQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        await _goalRepo.DidNotReceive().GetActiveCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoActiveGoals_ReturnsZero()
    {
        var profile = UserProfileEntity.Create();
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goalRepo.GetActiveCountAsync(profile.UserId, Arg.Any<CancellationToken>()).Returns(0);

        var result = await _handler.Handle(new GetActiveGoalCountQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    [Fact]
    public async Task HasActiveGoals_ReturnsCount()
    {
        var profile = UserProfileEntity.Create();
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goalRepo.GetActiveCountAsync(profile.UserId, Arg.Any<CancellationToken>()).Returns(2);

        var result = await _handler.Handle(new GetActiveGoalCountQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
    }
}
