namespace LifeGrid.Application.MomentBurst;

public abstract record MomentBurstResult
{
    public sealed record Accepted(
        string QuestName,
        string Description,
        double MeasureValue,
        string MeasureUnit) : MomentBurstResult;

    public sealed record Denied(string Message) : MomentBurstResult;
}
