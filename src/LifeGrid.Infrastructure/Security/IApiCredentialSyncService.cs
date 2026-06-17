namespace LifeGrid.Infrastructure.Security;

public interface IApiCredentialSyncService
{
    Task SyncAsync(CancellationToken ct = default);
}
