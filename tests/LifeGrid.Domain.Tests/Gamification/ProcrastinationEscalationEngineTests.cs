using FluentAssertions;
using LifeGrid.Domain.Gamification;
using LifeGrid.Domain.WeekGoal;

namespace LifeGrid.Domain.Tests.Gamification;

public sealed class ProcrastinationEscalationEngineTests
{
    // ── Clean state ────────────────────────────────────────────────────────────

    [Fact]
    public void Clean_GpAt79_TransitionsToLevel1Warning()
    {
        var result = ProcrastinationEscalationEngine.Evaluate(PenaltyState.Clean, 79.0, 100);

        result.NewPenaltyState.Should().Be(PenaltyState.Level1Warning);
        result.PenalizedXp.Should().Be(100);
        result.TriggersOverwhelmed.Should().BeFalse();
    }

    [Fact]
    public void Clean_GpAt80_TransitionsToLevel1Warning_BoundaryInclusive()
    {
        var result = ProcrastinationEscalationEngine.Evaluate(PenaltyState.Clean, 80.0, 50);

        result.NewPenaltyState.Should().Be(PenaltyState.Level1Warning);
        result.PenalizedXp.Should().Be(50);
    }

    [Fact]
    public void Clean_GpAt81_RemainsClean()
    {
        var result = ProcrastinationEscalationEngine.Evaluate(PenaltyState.Clean, 81.0, 200);

        result.NewPenaltyState.Should().Be(PenaltyState.Clean);
        result.PenalizedXp.Should().Be(200);
        result.TriggersOverwhelmed.Should().BeFalse();
    }

    // ── Level1Warning state ────────────────────────────────────────────────────

    [Fact]
    public void Level1Warning_GpAt100_ClearsToClean()
    {
        var result = ProcrastinationEscalationEngine.Evaluate(PenaltyState.Level1Warning, 100.0, 80);

        result.NewPenaltyState.Should().Be(PenaltyState.Clean);
        result.PenalizedXp.Should().Be(80);
        result.TriggersOverwhelmed.Should().BeFalse();
    }

    [Fact]
    public void Level1Warning_GpAt95_100Xp_SetsProbation_HalvesXp()
    {
        var result = ProcrastinationEscalationEngine.Evaluate(PenaltyState.Level1Warning, 95.0, 100);

        result.NewPenaltyState.Should().Be(PenaltyState.ProbationWeek2);
        result.PenalizedXp.Should().Be(50);
        result.TriggersOverwhelmed.Should().BeFalse();
    }

    [Fact]
    public void Level1Warning_OddXp_SetsProbation_FloorHalves()
    {
        var result = ProcrastinationEscalationEngine.Evaluate(PenaltyState.Level1Warning, 50.0, 7);

        result.NewPenaltyState.Should().Be(PenaltyState.ProbationWeek2);
        result.PenalizedXp.Should().Be(3);
    }

    // ── ProbationWeek2 state ───────────────────────────────────────────────────

    [Fact]
    public void ProbationWeek2_GpAt100_ClearsToClean()
    {
        var result = ProcrastinationEscalationEngine.Evaluate(PenaltyState.ProbationWeek2, 100.0, 60);

        result.NewPenaltyState.Should().Be(PenaltyState.Clean);
        result.PenalizedXp.Should().Be(60);
        result.TriggersOverwhelmed.Should().BeFalse();
    }

    [Fact]
    public void ProbationWeek2_GpAt99_TriggersReckoning_ZerosXp()
    {
        var result = ProcrastinationEscalationEngine.Evaluate(PenaltyState.ProbationWeek2, 99.0, 200);

        result.NewPenaltyState.Should().Be(PenaltyState.ReckoningWeek3);
        result.PenalizedXp.Should().Be(0);
        result.TriggersOverwhelmed.Should().BeTrue();
    }
}
