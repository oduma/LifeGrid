using FluentAssertions;
using LifeGrid.Domain.Gamification;

namespace LifeGrid.Domain.Tests.Gamification;

public sealed class ShieldEconomyEngineTests
{
    // ── ApplySpGain — normal conversion ──────────────────────────────────────

    [Fact]
    public void ApplySpGain_BelowThreshold_NoShieldGranted()
    {
        var (sp, shields) = ShieldEconomyEngine.ApplySpGain(
            currentSp: 15, shieldsAvailable: 0, maxShieldCap: 2, amount: 10);

        sp.Should().Be(25);
        shields.Should().Be(0);
    }

    [Fact]
    public void ApplySpGain_HitsThreshold_GrantsOneShieldAndResetsRemainder()
    {
        var (sp, shields) = ShieldEconomyEngine.ApplySpGain(
            currentSp: 25, shieldsAvailable: 0, maxShieldCap: 2, amount: 10);

        sp.Should().Be(5);
        shields.Should().Be(1);
    }

    [Fact]
    public void ApplySpGain_MultipleThresholds_GrantsMultipleShields()
    {
        // 0 + 65 = 65 → two conversions (65 - 60 = 5), shields = 2
        var (sp, shields) = ShieldEconomyEngine.ApplySpGain(
            currentSp: 0, shieldsAvailable: 0, maxShieldCap: 2, amount: 65);

        sp.Should().Be(5);
        shields.Should().Be(2);
    }

    // ── ApplySpGain — max shield cap ─────────────────────────────────────────

    [Fact]
    public void ApplySpGain_AtMaxShieldCap_CapsSpAt30_NoConversion()
    {
        // TDD invariant 3: SP hits 30 but shields == maxCap → blocked, SP stays at 30
        var (sp, shields) = ShieldEconomyEngine.ApplySpGain(
            currentSp: 25, shieldsAvailable: 2, maxShieldCap: 2, amount: 10);

        sp.Should().Be(30, "SP is held at the conversion threshold when at max shield cap");
        shields.Should().Be(2, "no new shield is granted");
    }

    [Fact]
    public void ApplySpGain_AboveThresholdAtMaxCap_CapsAt30NotHigher()
    {
        // Large gain while at max cap should not accumulate above 30
        var (sp, shields) = ShieldEconomyEngine.ApplySpGain(
            currentSp: 0, shieldsAvailable: 2, maxShieldCap: 2, amount: 40);

        sp.Should().Be(30, "SP is capped at 30 regardless of how much was earned at max cap");
        shields.Should().Be(2);
    }

    // ── ApplySpDeduction — Deep Deficit ──────────────────────────────────────

    [Fact]
    public void ApplySpDeduction_ResultsInNegativeDeepDeficit()
    {
        // TDD invariant 1: cheating penalty −30 on SP=10 → SP=−20
        var sp = ShieldEconomyEngine.ApplySpDeduction(currentSp: 10, amount: 30);

        sp.Should().Be(-20, "cheating penalty drives SP into Deep Deficit");
    }

    [Fact]
    public void ApplySpGain_Recovery_From_Minus20_Requires20SpToReachZero()
    {
        // TDD invariant 2: −20 SP profile must accumulate exactly 20 SP to reach 0
        var (sp, shields) = ShieldEconomyEngine.ApplySpGain(
            currentSp: -20, shieldsAvailable: 0, maxShieldCap: 2, amount: 20);

        sp.Should().Be(0, "deficit is exactly cleared; no overshoot into conversion territory");
        shields.Should().Be(0, "SP=0 is below the 30 threshold — no shield is granted yet");
    }
}
