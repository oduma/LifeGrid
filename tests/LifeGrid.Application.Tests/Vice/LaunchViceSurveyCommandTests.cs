using FluentAssertions;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Vice;
using NSubstitute;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Application.Tests.Vice;

public sealed class LaunchViceSurveyCommandTests
{
    private readonly IUserProfileRepository    _userProfiles = Substitute.For<IUserProfileRepository>();
    private readonly LaunchViceSurveyCommandHandler _handler;

    public LaunchViceSurveyCommandTests()
        => _handler = new LaunchViceSurveyCommandHandler(_userProfiles);

    [Fact]
    public async Task NoProfile_ReturnsSuccess()
    {
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns((UserProfileEntity?)null);

        var result = await _handler.Handle(new LaunchViceSurveyCommand(), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task NotCompleted_ReturnsSuccess()
    {
        var profile = UserProfileEntity.Create();
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _handler.Handle(new LaunchViceSurveyCommand(), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AlreadyCompleted_ReturnsFailure()
    {
        var profile = UserProfileEntity.Create();
        profile.GrantSurveyBonusShield(); // sets IsViceSurveyCompleted = true
        _userProfiles.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _handler.Handle(new LaunchViceSurveyCommand(), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("already_completed");
    }
}
