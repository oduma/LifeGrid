using LifeGrid.Domain.WeekGoal;

namespace LifeGrid.Domain.Gamification;

public record EscalationResult(PenaltyState NewPenaltyState, int PenalizedXp, bool TriggersOverwhelmed);

public static class ProcrastinationEscalationEngine
{
    public static EscalationResult Evaluate(
        PenaltyState currentState,
        double       goalWeeklyGp,
        int          currentXpEarned)
        => currentState switch
        {
            PenaltyState.Clean => goalWeeklyGp <= 80.0
                ? new(PenaltyState.Level1Warning,   currentXpEarned, false)
                : new(PenaltyState.Clean,            currentXpEarned, false),

            PenaltyState.Level1Warning => goalWeeklyGp >= 100.0
                ? new(PenaltyState.Clean,            currentXpEarned, false)
                : new(PenaltyState.ProbationWeek2,   (int)Math.Floor(currentXpEarned / 2.0), false),

            PenaltyState.ProbationWeek2 => goalWeeklyGp >= 100.0
                ? new(PenaltyState.Clean,            currentXpEarned, false)
                : new(PenaltyState.ReckoningWeek3,   0, true),

            _ => new(currentState, currentXpEarned, false)
        };
}
