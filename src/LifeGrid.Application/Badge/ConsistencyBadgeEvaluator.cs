using LifeGrid.Application.Common;
using LifeGrid.Application.Habit;
using LifeGrid.Domain.Badge;
using BadgeEntity = LifeGrid.Domain.Badge.Badge;

namespace LifeGrid.Application.Badge;

public sealed class ConsistencyBadgeEvaluator(
    ILoginHistoryRepository loginHistoryRepository,
    IHabitRepository        habitRepository,
    IBadgeRepository        badgeRepository,
    IUnitOfWork             unitOfWork,
    IDateTimeProvider       clock)
    : IConsistencyBadgeEvaluator
{
    private const string EventAvailableGlyph = "";

    public async Task<IReadOnlyCollection<BadgeDto>> EvaluateAsync(Guid userId, CancellationToken ct)
    {
        var timestamps = await loginHistoryRepository.GetTimestampsByUserIdAsync(userId, ct);
        if (timestamps.Count < 7)
            return Array.Empty<BadgeDto>();

        var earnedBadges = await badgeRepository.GetEarnedByUserIdAsync(userId, ct);
        var earnedTiers  = earnedBadges.Select(b => b.Tier).ToHashSet();
        if (earnedTiers.Count == 3)
            return Array.Empty<BadgeDto>();

        var loginDays   = timestamps.Select(t => t.Date).ToHashSet();
        var weekMondays = GetCompletedWeekMondays(loginDays, clock.UtcNow.Date);
        var newBadges   = new List<BadgeEntity>();

        foreach (var monday in weekMondays)
        {
            if (earnedTiers.Count == 3) break;

            var weekDays = Enumerable.Range(0, 7).Select(i => monday.AddDays(i)).ToList();
            if (!weekDays.All(d => loginDays.Contains(d))) continue;

            var thursday    = monday.AddDays(3);
            var sundayNext  = monday.AddDays(7);

            bool hasMonWed = await habitRepository.HasCompletionLogsInRangeAsync(
                monday.ToUtcStart(), thursday.ToUtcStart(), ct);
            bool hasThuSun = await habitRepository.HasCompletionLogsInRangeAsync(
                thursday.ToUtcStart(), sundayNext.ToUtcStart(), ct);

            var tier = hasMonWed ? BadgeTier.Gold
                     : hasThuSun ? BadgeTier.Silver
                     : BadgeTier.Bronze;

            if (earnedTiers.Contains(tier)) continue;

            var dateEarned = monday.AddDays(6).ToUtcEndOfDay();
            var badge = CreateBadge(userId, tier, dateEarned);

            await badgeRepository.AddAsync(badge, ct);
            earnedTiers.Add(tier);
            newBadges.Add(badge);
        }

        if (newBadges.Count > 0)
            await unitOfWork.CommitAsync(ct);

        return newBadges
            .Select(b => new BadgeDto(
                b.BadgeId, b.BadgeType, b.BadgeName, b.IconName,
                b.Description, b.Tier, b.IsEarned, b.DateEarned))
            .ToArray();
    }

    private static BadgeEntity CreateBadge(Guid userId, BadgeTier tier, DateTime dateEarned)
    {
        var dateLabel = dateEarned.ToString("dd MMM yyyy");
        return tier switch
        {
            BadgeTier.Gold => BadgeEntity.CreateEarned(
                userId, "Showing_Up_Gold", "Mr. Consistency (Gold)",
                $"Logged in every day, first-half habits done. Achieved: {dateLabel}",
                EventAvailableGlyph, BadgeTier.Gold, dateEarned),
            BadgeTier.Silver => BadgeEntity.CreateEarned(
                userId, "Showing_Up_Silver", "Mr. Consistency (Silver)",
                $"Logged in every day, habits on schedule. Achieved: {dateLabel}",
                EventAvailableGlyph, BadgeTier.Silver, dateEarned),
            _ => BadgeEntity.CreateEarned(
                userId, "Showing_Up_Bronze", "Mr. Consistency (Bronze)",
                $"Logged in every day. Achieved: {dateLabel}",
                EventAvailableGlyph, BadgeTier.Bronze, dateEarned)
        };
    }

    private static IEnumerable<DateTime> GetCompletedWeekMondays(HashSet<DateTime> loginDays, DateTime today)
    {
        if (loginDays.Count == 0) return Enumerable.Empty<DateTime>();

        var earliest = loginDays.Min();
        var monday   = ToMonday(earliest);
        var result   = new List<DateTime>();

        while (monday.AddDays(6) < today)
        {
            result.Add(monday);
            monday = monday.AddDays(7);
        }

        return result;
    }

    private static DateTime ToMonday(DateTime date)
    {
        int daysFromMonday = date.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)date.DayOfWeek - 1;
        return date.Date.AddDays(-daysFromMonday);
    }
}
