using FluentAssertions;
using LifeGrid.Application.Badge;
using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Badge;
using NSubstitute;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;
using BadgeEntity = LifeGrid.Domain.Badge.Badge;

namespace LifeGrid.Application.Tests.Badge;

public sealed class GetUserBadgesQueryTests
{
    private readonly IUserProfileRepository _profileRepo = Substitute.For<IUserProfileRepository>();
    private readonly IBadgeRepository       _badgeRepo   = Substitute.For<IBadgeRepository>();
    private readonly GetUserBadgesQueryHandler _handler;

    private static readonly DateTime Fixed = new(2026, 6, 24, 23, 59, 59, DateTimeKind.Utc);

    public GetUserBadgesQueryTests()
    {
        _handler = new GetUserBadgesQueryHandler(_badgeRepo, _profileRepo);
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
        _badgeRepo.GetEarnedByUserIdAsync(profile.UserId, Arg.Any<CancellationToken>())
                  .Returns(Array.Empty<BadgeEntity>());

        var result = await _handler.Handle(new GetUserBadgesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task ProfileWithBadges_ReturnsMappedDtos()
    {
        var profile = UserProfileEntity.Create();
        var badge   = BadgeEntity.CreateEarned(
            profile.UserId, "Showing_Up_Bronze", "Mr. Consistency (Bronze)",
            "Logged in every day. Achieved: 24 Jun 2026",
            "", BadgeTier.Bronze, Fixed);

        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _badgeRepo.GetEarnedByUserIdAsync(profile.UserId, Arg.Any<CancellationToken>())
                  .Returns(new[] { badge });

        var result = await _handler.Handle(new GetUserBadgesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);

        var dto = result.Value!.Single();
        dto.BadgeType.Should().Be("Showing_Up_Bronze");
        dto.BadgeName.Should().Be("Mr. Consistency (Bronze)");
        dto.Tier.Should().Be(BadgeTier.Bronze);
        dto.IsEarned.Should().BeTrue();
        dto.DateEarned.Should().Be(Fixed);
    }
}
