namespace LifeGrid.Domain.Gamification;

public static class ShieldEconomyEngine
{
    public const int SpConversionThreshold = 30;

    // Applies a positive SP gain. Converts complete 30 SP milestones into shields
    // while shields < maxShieldCap. At max cap, SP is held at exactly 30 —
    // no conversion, no accumulation above the threshold (preserves progress).
    public static (int NewCurrentSp, int NewShieldsAvailable) ApplySpGain(
        int currentSp, int shieldsAvailable, int maxShieldCap, int amount)
    {
        var sp = currentSp + amount;
        while (sp >= SpConversionThreshold && shieldsAvailable < maxShieldCap)
        {
            sp -= SpConversionThreshold;
            shieldsAvailable++;
        }
        if (shieldsAvailable >= maxShieldCap && sp > SpConversionThreshold)
            sp = SpConversionThreshold;
        return (sp, shieldsAvailable);
    }

    // Applies a negative SP mutation (cheating penalty §3.5).
    // The result MAY be negative — this is the intentional Deep Deficit state.
    // Recovery requires logging positive SP to return to >= 0 before conversion resumes.
    public static int ApplySpDeduction(int currentSp, int amount)
        => currentSp - amount;
}
