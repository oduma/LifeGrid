namespace LifeGrid.Application.Week;

public abstract record BlueprintResult
{
    public sealed record Feasible(string BlueprintJson) : BlueprintResult;

    public sealed record Infeasible(
        string  Reason,
        string? SuggestedDeadline,
        string? SuggestedScope) : BlueprintResult;
}
