using System.Text;

namespace LifeGrid.Infrastructure.Security;

internal sealed class ApiCredentialSyncService : IApiCredentialSyncService
{
    private const string TokenKey = "Gemini_Provider_Token";

    private readonly ISecureStorageService _secureStorage;
    private readonly IBuildSecretProvider  _buildSecretProvider;

    public ApiCredentialSyncService(
        ISecureStorageService secureStorage,
        IBuildSecretProvider  buildSecretProvider)
    {
        _secureStorage       = secureStorage;
        _buildSecretProvider = buildSecretProvider;
    }

    public async Task SyncAsync(CancellationToken ct = default)
    {
        var existing = await _secureStorage.GetAsync(TokenKey);
        if (existing != null)
            return; // Case A: already seeded

        // Case B: first launch — decode and write to secure storage
        var obfuscated = _buildSecretProvider.GetObfuscatedBytes();
        var salt       = _buildSecretProvider.GetXorSalt();

        byte[]? decoded = new byte[obfuscated.Length];
        for (int i = 0; i < obfuscated.Length; i++)
            decoded[i] = (byte)(obfuscated[i] ^ salt);

        var plaintext = Encoding.UTF8.GetString(decoded);
        await _secureStorage.SetAsync(TokenKey, plaintext);

        Array.Clear(decoded, 0, decoded.Length);
        decoded = null;
    }
}
