using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Hud;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Week;
using NSubstitute;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;
using WeekEntity        = LifeGrid.Domain.Week.Week;
using WeekGoalEntity    = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Application.Tests.Hud;

public sealed class GetHudTelemetryQueryTests
{
    private readonly IUserProfileRepository      _profileRepo = Substitute.For<IUserProfileRepository>();
    private readonly IWeekRepository             _weekRepo    = Substitute.For<IWeekRepository>();
    private readonly IDateTimeProvider           _clock       = Substitute.For<IDateTimeProvider>();
    private readonly GetHudTelemetryQueryHandler _handler;

    // A known Monday so the current-Monday calculation is deterministic in tests
    private static readonly DateTime FixedMonday =
        new(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc); // Monday

    public GetHudTelemetryQueryTests()
    {
        _clock.UtcNow.Returns(FixedMonday);
        _handler = new GetHudTelemetryQueryHandler(_profileRepo, _weekRepo, _clock);
    }

    [Fact]
    public async Task NoProfile_ReturnsAllZeroDto()
    {
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns((UserProfileEntity?)null);

        var result = await _handler.Handle(new GetHudTelemetryQuery(), default);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!;
        dto.Level.Should().Be(0);
        dto.LifetimeGp.Should().Be(0.0);
        dto.WeeklyGp.Should().Be(0.0);
        dto.LifetimeXp.Should().Be(0);
        dto.WeeklyXp.Should().Be(0);
        dto.CurrentSp.Should().Be(0);
        dto.WeeklySpEarned.Should().Be(0);
        dto.ActiveShields.Should().Be(0);
    }

    [Fact]
    public async Task WithProfileNoCurrentWeek_ReturnsLifetimeMetrics_WeeklyAllZero()
    {
        var profile = UserProfileEntity.Create();
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _weekRepo.GetByStartDateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns((WeekEntity?)null);

        var result = await _handler.Handle(new GetHudTelemetryQuery(), default);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!;
        dto.Level.Should().Be(profile.CurrentLevel);
        dto.LifetimeXp.Should().Be(profile.Economy.LifetimeXp);
        dto.CurrentSp.Should().Be(profile.Economy.CurrentSp);
        dto.ShieldCap.Should().Be(profile.Economy.MaxShieldCap);
        dto.WeeklyGp.Should().Be(0.0);
        dto.WeeklyXp.Should().Be(0);
        dto.WeeklySpEarned.Should().Be(0);
    }

    [Fact]
    public async Task WithMultipleWeekGoals_AveragesGpCorrectly()
    {
        var profile = UserProfileEntity.Create();
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var week = WeekEntity.Create(1, FixedMonday);
        var wg1  = WeekGoalEntity.Create(week.WeekId, Guid.NewGuid(), 1);
        var wg2  = WeekGoalEntity.Create(week.WeekId, Guid.NewGuid(), 2);
        wg1.SetGoalWeeklyGp(2.0);
        wg2.SetGoalWeeklyGp(4.0);
        week.AddWeekGoal(wg1);
        week.AddWeekGoal(wg2);
        _weekRepo.GetByStartDateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns(week);

        var result = await _handler.Handle(new GetHudTelemetryQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.WeeklyGp.Should().Be(3.0);
    }

    [Fact]
    public async Task WithMultipleWeekGoals_SumsXpCorrectly()
    {
        var profile = UserProfileEntity.Create();
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var week = WeekEntity.Create(1, FixedMonday);
        var wg1  = WeekGoalEntity.Create(week.WeekId, Guid.NewGuid(), 1);
        var wg2  = WeekGoalEntity.Create(week.WeekId, Guid.NewGuid(), 2);
        wg1.SetGoalWeeklyXpEarned(100);
        wg2.SetGoalWeeklyXpEarned(150);
        week.AddWeekGoal(wg1);
        week.AddWeekGoal(wg2);
        _weekRepo.GetByStartDateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns(week);

        var result = await _handler.Handle(new GetHudTelemetryQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.WeeklyXp.Should().Be(250);
    }

    [Fact]
    public async Task Handler_RequestsCurrentMondayFromDateProvider()
    {
        // Wednesday — handler should strip back to Monday
        _clock.UtcNow.Returns(new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc));
        var profile = UserProfileEntity.Create();
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _weekRepo.GetByStartDateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns((WeekEntity?)null);

        await _handler.Handle(new GetHudTelemetryQuery(), default);

        await _weekRepo.Received(1)
            .GetByStartDateAsync(
                new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc), // Monday
                Arg.Any<CancellationToken>());
    }
}
