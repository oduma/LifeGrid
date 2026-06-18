namespace LifeGrid.Domain.Goal;

public sealed class GoalRefinementAnswer
{
    private GoalRefinementAnswer() { }

    internal static GoalRefinementAnswer Create(int rankOrder, string question, string? answer) => new()
    {
        RefinementAnswerId = Guid.NewGuid(),
        RankOrder          = rankOrder,
        Question           = question,
        Answer             = answer
    };

    public Guid    RefinementAnswerId { get; private set; }
    public int     RankOrder          { get; private set; }
    public string  Question           { get; private set; } = null!;
    public string? Answer             { get; private set; }
}
