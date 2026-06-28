namespace LifeGrid.Application.Week;

public interface IWeekLifecycleSyncService
{
    Task EvaluateAsync(CancellationToken ct = default);
}
