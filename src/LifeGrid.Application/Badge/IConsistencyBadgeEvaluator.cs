namespace LifeGrid.Application.Badge;

public interface IConsistencyBadgeEvaluator
{
    Task<IReadOnlyCollection<BadgeDto>> EvaluateAsync(Guid userId, CancellationToken ct = default);
}
