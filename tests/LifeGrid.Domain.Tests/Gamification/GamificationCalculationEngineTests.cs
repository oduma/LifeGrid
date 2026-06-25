using FluentAssertions;
using LifeGrid.Domain.Gamification;
using LifeGrid.Domain.Habit;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Domain.Tests.Gamification;

public sealed class GamificationCalculationEngineTests
{
    // ── CalculateEntryReward ──────────────────────────────────────────────────

    [Fact]
    public void CalculateEntryReward_ProvenAbove75pct_Returns20Xp4Sp()
    {
        var reward = GamificationCalculationEngine.CalculateEntryReward(
            HabitType.Planned, actualValue: 4, targetValue: 5, hasProof: true);

        reward.XpEarned.Should().Be(20);
        reward.SpEarned.Should().Be(4);
    }

    [Fact]
    public void CalculateEntryReward_ProvenExactly75pct_IsProven()
    {
        var reward = GamificationCalculationEngine.CalculateEntryReward(
            HabitType.Planned, actualValue: 3, targetValue: 4, hasProof: true);

        reward.XpEarned.Should().Be(20);
        reward.SpEarned.Should().Be(4);
    }

    [Fact]
    public void CalculateEntryReward_PartiallyProvenBelow75pct_Returns10Xp2Sp()
    {
        var reward = GamificationCalculationEngine.CalculateEntryReward(
            HabitType.Planned, actualValue: 3, targetValue: 5, hasProof: true);

        reward.XpEarned.Should().Be(10);
        reward.SpEarned.Should().Be(2);
    }

    [Fact]
    public void CalculateEntryReward_Unproven_Returns3Xp1Sp()
    {
        var reward = GamificationCalculationEngine.CalculateEntryReward(
            HabitType.Planned, actualValue: 10, targetValue: 10, hasProof: false);

        reward.XpEarned.Should().Be(3);
        reward.SpEarned.Should().Be(1);
    }

    // ── CalculateWeekGoalGp ───────────────────────────────────────────────────

    [Fact]
    public void CalculateWeekGoalGp_ExcludesMomentBurstHabits()
    {
        var summaries = new List<(double, double, HabitType)>
        {
            (80, 100, HabitType.Planned),      // 80%
            (100, 100, HabitType.MomentBurst)  // excluded
        };

        var gp = GamificationCalculationEngine.CalculateWeekGoalGp(summaries);

        gp.Should().BeApproximately(80.0, precision: 0.001);
    }

    [Fact]
    public void CalculateWeekGoalGp_CapsIndividualHabitAt100()
    {
        var summaries = new List<(double, double, HabitType)>
        {
            (200, 100, HabitType.Planned)  // over-achieved → capped at 100%
        };

        var gp = GamificationCalculationEngine.CalculateWeekGoalGp(summaries);

        gp.Should().BeApproximately(100.0, precision: 0.001);
    }

    [Fact]
    public void CalculateWeekGoalGp_AveragesMultiplePlannedHabits()
    {
        var summaries = new List<(double, double, HabitType)>
        {
            (50,  100, HabitType.Planned),  // 50%
            (100, 100, HabitType.Planned)   // 100%
        };

        var gp = GamificationCalculationEngine.CalculateWeekGoalGp(summaries);

        gp.Should().BeApproximately(75.0, precision: 0.001);
    }

    // ── CalculateLevel ────────────────────────────────────────────────────────

    [Fact]
    public void CalculateLevel_Rollover_290XpPlus25_SetsLevel2()
    {
        int totalXp = 290 + 25; // = 315

        var level    = GamificationCalculationEngine.CalculateLevel(totalXp);
        int leftover = totalXp % GamificationCalculationEngine.LevelThresholdXp;

        level.Should().Be(2);
        leftover.Should().Be(15);
    }

    [Fact]
    public void CalculateLevel_ExactThreshold_AdvancesLevel()
    {
        var level = GamificationCalculationEngine.CalculateLevel(300);

        level.Should().Be(2);
    }

    [Fact]
    public void CalculateLevel_BelowThreshold_RemainsLevel1()
    {
        var level = GamificationCalculationEngine.CalculateLevel(299);

        level.Should().Be(1);
    }

    // ── UserEconomy.GrantSp (SP → Shield conversion) ─────────────────────────

    [Fact]
    public void UserEconomy_GrantSp_Milestone30_GrantsShield()
    {
        var profile = UserProfileEntity.Create();
        // Start: CurrentSp=28, ShieldsAvailable=0
        profile.GrantSp(28); // 28 < 30 → no shield yet

        profile.Economy.CurrentSp.Should().Be(28);
        profile.Economy.ShieldsAvailable.Should().Be(0);

        // Push over 30
        profile.GrantSp(5); // 33 → 1 shield granted, CurrentSp = 3

        profile.Economy.ShieldsAvailable.Should().Be(1);
        profile.Economy.CurrentSp.Should().Be(3);
    }

    [Fact]
    public void UserEconomy_GrantSp_DoesNotExceedShieldCap()
    {
        var profile = UserProfileEntity.Create(); // MaxShieldCap = 2
        profile.GrantSp(60); // → 2 shields + 0 SP (60 / 30 = 2 shields)

        profile.Economy.ShieldsAvailable.Should().Be(2);
        profile.Economy.CurrentSp.Should().Be(0);

        // Earn more SP — shields already capped; SP is held at 30 (not converted, not lost above threshold)
        profile.GrantSp(35); // → conversion blocked at max cap; SP capped at 30

        profile.Economy.ShieldsAvailable.Should().Be(2);
        profile.Economy.CurrentSp.Should().Be(30);
    }

    // ── MomentBurst triple reward ─────────────────────────────────────────────

    [Fact]
    public void CalculateEntryReward_MomentBurst_Proven_Returns60Xp12Sp()
    {
        var reward = GamificationCalculationEngine.CalculateEntryReward(
            HabitType.MomentBurst, actualValue: 4, targetValue: 4, hasProof: true);

        reward.XpEarned.Should().Be(60);
        reward.SpEarned.Should().Be(12);
    }

    [Fact]
    public void CalculateEntryReward_MomentBurst_PartiallyProven_Returns30Xp6Sp()
    {
        var reward = GamificationCalculationEngine.CalculateEntryReward(
            HabitType.MomentBurst, actualValue: 2, targetValue: 4, hasProof: true);

        reward.XpEarned.Should().Be(30);
        reward.SpEarned.Should().Be(6);
    }

    [Fact]
    public void CalculateEntryReward_MomentBurst_Unproven_Returns9Xp3Sp()
    {
        var reward = GamificationCalculationEngine.CalculateEntryReward(
            HabitType.MomentBurst, actualValue: 4, targetValue: 4, hasProof: false);

        reward.XpEarned.Should().Be(9);
        reward.SpEarned.Should().Be(3);
    }
}
