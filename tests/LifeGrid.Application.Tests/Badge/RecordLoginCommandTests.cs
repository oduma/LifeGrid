using FluentAssertions;
using LifeGrid.Application.Badge;
using LifeGrid.Application.Common;
using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Badge;
using NSubstitute;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Application.Tests.Badge;

public sealed class RecordLoginCommandTests
{
    private readonly IUserProfileRepository    _profileRepo   = Substitute.For<IUserProfileRepository>();
    private readonly ILoginHistoryRepository   _loginRepo     = Substitute.For<ILoginHistoryRepository>();
    private readonly IConsistencyBadgeEvaluator _evaluator    = Substitute.For<IConsistencyBadgeEvaluator>();
    private readonly IUnitOfWork               _uow           = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider         _clock         = Substitute.For<IDateTimeProvider>();
    private readonly RecordLoginCommandHandler  _handler;

    private static readonly DateTime Now = new(2026, 6, 24, 9, 0, 0, DateTimeKind.Utc);

    public RecordLoginCommandTests()
    {
        _clock.UtcNow.Returns(Now);
        _evaluator.EvaluateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                  .Returns(Array.Empty<BadgeDto>());
        _handler = new RecordLoginCommandHandler(
            _profileRepo, _loginRepo, _evaluator, _uow, _clock);
    }

    [Fact]
    public async Task NullProfile_ReturnsSuccessWithEmptyBadges()
    {
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>())
                    .Returns((UserProfileEntity?)null);

        var result = await _handler.Handle(new RecordLoginCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        await _loginRepo.DidNotReceive().AddAsync(
            Arg.Any<LoginHistory>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidProfile_InsertsLoginHistoryEntry()
    {
        var profile = UserProfileEntity.Create();
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);

        await _handler.Handle(new RecordLoginCommand(), default);

        await _loginRepo.Received(1).AddAsync(
            Arg.Is<LoginHistory>(l => l.UserId == profile.UserId && l.Timestamp == Now),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidProfile_EvaluatorResultIsReturned()
    {
        var profile = UserProfileEntity.Create();
        var earned  = new BadgeDto(
            Guid.NewGuid(), "Showing_Up_Bronze", "Mr. Consistency (Bronze)",
            "", "Logged in every day. Achieved: 24 Jun 2026",
            BadgeTier.Bronze, true,
            new DateTime(2026, 6, 22, 23, 59, 59, DateTimeKind.Utc));

        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _evaluator.EvaluateAsync(profile.UserId, Arg.Any<CancellationToken>())
                  .Returns(new[] { earned });

        var result = await _handler.Handle(new RecordLoginCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value!.Single().BadgeType.Should().Be("Showing_Up_Bronze");
    }
}
