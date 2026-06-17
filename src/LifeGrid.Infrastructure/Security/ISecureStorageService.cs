namespace LifeGrid.Infrastructure.Security;

public interface ISecureStorageService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
}
