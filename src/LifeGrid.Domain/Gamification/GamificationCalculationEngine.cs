using LifeGrid.Domain.Habit;

namespace LifeGrid.Domain.Gamification;

public static class GamificationCalculationEngine
{
    public const int LevelThresholdXp = 300;

    public static EntryReward CalculateEntryReward(
        HabitType habitType, double actualValue, double targetValue, bool hasProof)
    {
        // Flash / DoubleXpMode multiplier deferred to a later phase
        var tier       = DetermineProofTier(actualValue, targetValue, hasProof);
        int multiplier = habitType == HabitType.MomentBurst ? 3 : 1;
        return tier switch
        {
            ProofTier.Proven          => new EntryReward(20 * multiplier, 4 * multiplier),
            ProofTier.PartiallyProven => new EntryReward(10 * multiplier, 2 * multiplier),
            _                         => new EntryReward( 3 * multiplier, 1 * multiplier)
        };
    }

    // GP for a single habit: cumulative completion capped at 100 (stored as 0–100 float)
    public static double CalculateHabitGp(double cumulativeTotalActual, double targetValue)
        => targetValue <= 0 ? 0.0 : Math.Min(cumulativeTotalActual / targetValue * 100.0, 100.0);

    // WeekGoal GP: average of all non-MomentBurst habits (Section 4.3.1)
    public static double CalculateWeekGoalGp(
        IReadOnlyList<(double CumulativeTotal, double TargetValue, HabitType HabitType)> habitSummaries)
    {
        var eligible = habitSummaries
            .Where(h => h.HabitType != HabitType.MomentBurst)
            .ToList();

        if (eligible.Count == 0) return 0.0;

        return eligible.Average(h => CalculateHabitGp(h.CumulativeTotal, h.TargetValue));
    }

    // Level = lifetimeXp / 300 + 1 (integer division, minimum 1)
    public static int CalculateLevel(int lifetimeXp, int levelThreshold = LevelThresholdXp)
        => Math.Max(1, lifetimeXp / levelThreshold + 1);

    private static ProofTier DetermineProofTier(double actualValue, double targetValue, bool hasProof)
    {
        if (!hasProof) return ProofTier.Unproven;
        var ratio = targetValue > 0 ? actualValue / targetValue : 0.0;
        return ratio >= 0.75 ? ProofTier.Proven : ProofTier.PartiallyProven;
    }
}
