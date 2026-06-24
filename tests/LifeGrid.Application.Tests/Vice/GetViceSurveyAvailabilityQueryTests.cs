using FluentAssertions;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Vice;
using NSubstitute;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Application.Tests.Vice;

public sealed class GetViceSurveyAvailabilityQueryTests
{
    private readonly IUserProfileRepository              _profileRepo = Substitute.For<IUserProfileRepository>();
    private readonly GetViceSurveyAvailabilityQueryHandler _handler;

    public GetViceSurveyAvailabilityQueryTests()
        => _handler = new GetViceSurveyAvailabilityQueryHandler(_profileRepo);

    [Fact]
    public async Task NoProfile_ReturnsFalse()
    {
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns((UserProfileEntity?)null);

        var result = await _handler.Handle(new GetViceSurveyAvailabilityQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task SurveyCompleted_ReturnsFalse()
    {
        var profile = UserProfileEntity.Create();
        profile.GrantSurveyBonusShield(); // marks IsViceSurveyCompleted = true
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _handler.Handle(new GetViceSurveyAvailabilityQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task SurveyNotCompleted_ReturnsTrue()
    {
        var profile = UserProfileEntity.Create();
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _handler.Handle(new GetViceSurveyAvailabilityQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }
}
