namespace LifeGrid.Application.Common;

public interface IFactoryResetService
{
    Task ResetAsync(CancellationToken ct = default);
}
