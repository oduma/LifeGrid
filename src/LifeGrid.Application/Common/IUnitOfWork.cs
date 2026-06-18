namespace LifeGrid.Application.Common;

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken ct = default);
}
