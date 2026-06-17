namespace LifeGrid.Infrastructure.Security;

internal sealed class SecureStorageService : ISecureStorageService
{
    public Task<string?> GetAsync(string key) =>
        Microsoft.Maui.Storage.SecureStorage.GetAsync(key);

    public Task SetAsync(string key, string value) =>
        Microsoft.Maui.Storage.SecureStorage.SetAsync(key, value);
}
