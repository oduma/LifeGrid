using FluentAssertions;
using LifeGrid.Application.Badge;
using LifeGrid.Application.UserProfile;
using NSubstitute;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Application.Tests.Badge;

public sealed class GetUserBadgesQueryTests
{
    private readonly IUserProfileRepository    _profileRepo = Substitute.For<IUserProfileRepository>();
    private readonly GetUserBadgesQueryHandler _handler;

    private static readonly DateTime Fixed = new(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc);

    public GetUserBadgesQueryTests()
    {
        _handler = new GetUserBadgesQueryHandler(_profileRepo);
    }

    [Fact]
    public async Task NullProfile_ReturnsEmptyCollection()
    {
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>())
                    .Returns((UserProfileEntity?)null);

        var result = await _handler.Handle(new GetUserBadgesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task ProfileWithNoBadges_ReturnsEmptyCollection()
    {
        var profile = UserProfileEntity.Create();
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _handler.Handle(new GetUserBadgesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task ProfileWithBadges_ReturnsMappedDtos()
    {
        var profile = UserProfileEntity.Create();
        profile.AwardBadge("Showing_Up_Gold", "Seven-day streak.", "", Fixed);
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _handler.Handle(new GetUserBadgesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);

        var dto = result.Value!.Single();
        dto.BadgeType.Should().Be("Showing_Up_Gold");
        dto.IconName.Should().Be("");
        dto.Description.Should().Be("Seven-day streak.");
        dto.DateEarned.Should().Be(Fixed);
    }
}
