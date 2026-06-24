using FluentAssertions;
using LifeGrid.Application.Badge;
using LifeGrid.Application.Common;
using LifeGrid.Application.Habit;
using LifeGrid.Domain.Badge;
using NSubstitute;
using BadgeEntity = LifeGrid.Domain.Badge.Badge;

namespace LifeGrid.Application.Tests.Badge;

public sealed class ConsistencyBadgeEvaluatorTests
{
    private readonly ILoginHistoryRepository  _loginRepo   = Substitute.For<ILoginHistoryRepository>();
    private readonly IHabitRepository         _habitRepo   = Substitute.For<IHabitRepository>();
    private readonly IBadgeRepository         _badgeRepo   = Substitute.For<IBadgeRepository>();
    private readonly IUnitOfWork              _uow         = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider        _clock       = Substitute.For<IDateTimeProvider>();
    private readonly ConsistencyBadgeEvaluator _evaluator;

    private static readonly Guid UserId = Guid.NewGuid();

    public ConsistencyBadgeEvaluatorTests()
    {
        _evaluator = new ConsistencyBadgeEvaluator(
            _loginRepo, _habitRepo, _badgeRepo, _uow, _clock);

        _badgeRepo.GetEarnedByUserIdAsync(UserId, Arg.Any<CancellationToken>())
                  .Returns(Array.Empty<BadgeEntity>());
        _habitRepo.HasCompletionLogsInRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                  .Returns(false);
    }

    // Week Mon 15 Jun → Sun 21 Jun 2026 (all complete, today = 24 Jun)
    private static readonly DateTime Monday = new(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
    private static List<DateTime> FullWeekLogins() =>
        Enumerable.Range(0, 7).Select(i => Monday.AddDays(i)).ToList();

    private void SetToday(DateTime today) => _clock.UtcNow.Returns(today);
    private void SetLogins(List<DateTime> logins) =>
        _loginRepo.GetTimestampsByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(logins);

    [Fact]
    public async Task NoLogins_ReturnsEmpty()
    {
        SetToday(new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc));
        SetLogins(new List<DateTime>());

        var result = await _evaluator.EvaluateAsync(UserId, default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FewerThanSevenLogins_ReturnsEmpty()
    {
        SetToday(new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc));
        SetLogins(Enumerable.Range(0, 6).Select(i => Monday.AddDays(i)).ToList());

        var result = await _evaluator.EvaluateAsync(UserId, default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AllSevenDays_NoHabits_AwardsBronze()
    {
        SetToday(new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc));
        SetLogins(FullWeekLogins());
        // habitRepo returns false by default (no logs in any range)

        var result = await _evaluator.EvaluateAsync(UserId, default);

        result.Should().HaveCount(1);
        result.Single().Tier.Should().Be(BadgeTier.Bronze);
        result.Single().BadgeType.Should().Be("Showing_Up_Bronze");
    }

    [Fact]
    public async Task AllSevenDays_HabitsOnlyThuSun_AwardsSilver()
    {
        SetToday(new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc));
        SetLogins(FullWeekLogins());

        var thursday = Monday.AddDays(3);
        var sundayNext = Monday.AddDays(7);

        // Mon-Wed: no logs; Thu-Sun: has logs
        _habitRepo.HasCompletionLogsInRangeAsync(
            Arg.Is<DateTime>(d => d == Monday),
            Arg.Is<DateTime>(d => d == thursday),
            Arg.Any<CancellationToken>())
            .Returns(false);
        _habitRepo.HasCompletionLogsInRangeAsync(
            Arg.Is<DateTime>(d => d == thursday),
            Arg.Is<DateTime>(d => d == sundayNext),
            Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _evaluator.EvaluateAsync(UserId, default);

        result.Should().HaveCount(1);
        result.Single().Tier.Should().Be(BadgeTier.Silver);
        result.Single().BadgeType.Should().Be("Showing_Up_Silver");
    }

    [Fact]
    public async Task AllSevenDays_HabitsIncludeMonWed_AwardsGold()
    {
        SetToday(new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc));
        SetLogins(FullWeekLogins());

        var thursday = Monday.AddDays(3);

        // Mon-Wed: has logs → Gold regardless of Thu-Sun
        _habitRepo.HasCompletionLogsInRangeAsync(
            Arg.Is<DateTime>(d => d == Monday),
            Arg.Is<DateTime>(d => d == thursday),
            Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _evaluator.EvaluateAsync(UserId, default);

        result.Should().HaveCount(1);
        result.Single().Tier.Should().Be(BadgeTier.Gold);
        result.Single().BadgeType.Should().Be("Showing_Up_Gold");
    }

    [Fact]
    public async Task TwoQualifyingWeeks_BronzeAndSilver_AwardsBoth()
    {
        // Week 1: 16–22 Jun → Bronze (no habits)
        // Week 2: 8–14 Jun → Silver (habits Thu-Sun only)
        var week2Monday   = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc);
        var week2Thursday = week2Monday.AddDays(3);
        var week2Next     = week2Monday.AddDays(7);

        SetToday(new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc));

        var logins = FullWeekLogins()
            .Concat(Enumerable.Range(0, 7).Select(i => week2Monday.AddDays(i)))
            .ToList();
        SetLogins(logins);

        // Week 2 Mon-Wed: no logs; Week 2 Thu-Sun: has logs
        _habitRepo.HasCompletionLogsInRangeAsync(
            Arg.Is<DateTime>(d => d == week2Thursday),
            Arg.Is<DateTime>(d => d == week2Next),
            Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _evaluator.EvaluateAsync(UserId, default);

        result.Should().HaveCount(2);
        result.Select(b => b.Tier).Should().BeEquivalentTo(new[] { BadgeTier.Bronze, BadgeTier.Silver });
    }

    [Fact]
    public async Task BronzeAlreadyEarned_QualifyingWeek_DoesNotDuplicate()
    {
        SetToday(new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc));
        SetLogins(FullWeekLogins());

        var existingBronze = BadgeEntity.CreateEarned(
            UserId, "Showing_Up_Bronze", "Mr. Consistency (Bronze)",
            "already earned", "", BadgeTier.Bronze,
            new DateTime(2026, 6, 15, 23, 59, 59, DateTimeKind.Utc));

        _badgeRepo.GetEarnedByUserIdAsync(UserId, Arg.Any<CancellationToken>())
                  .Returns(new[] { existingBronze });

        var result = await _evaluator.EvaluateAsync(UserId, default);

        result.Should().BeEmpty();
        await _badgeRepo.DidNotReceive().AddAsync(Arg.Any<BadgeEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AllTiersAlreadyEarned_ReturnsEmpty()
    {
        SetToday(new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc));
        SetLogins(FullWeekLogins());

        _badgeRepo.GetEarnedByUserIdAsync(UserId, Arg.Any<CancellationToken>())
                  .Returns(new[]
                  {
                      BadgeEntity.CreateEarned(UserId, "Showing_Up_Bronze", "b", "d", "", BadgeTier.Bronze, Fixed()),
                      BadgeEntity.CreateEarned(UserId, "Showing_Up_Silver", "s", "d", "", BadgeTier.Silver, Fixed()),
                      BadgeEntity.CreateEarned(UserId, "Showing_Up_Gold",   "g", "d", "", BadgeTier.Gold,   Fixed())
                  });

        var result = await _evaluator.EvaluateAsync(UserId, default);

        result.Should().BeEmpty();
    }

    private static DateTime Fixed() => new(2026, 6, 15, 23, 59, 59, DateTimeKind.Utc);
}
