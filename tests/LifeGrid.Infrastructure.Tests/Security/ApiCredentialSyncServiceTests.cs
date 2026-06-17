using FluentAssertions;
using LifeGrid.Infrastructure.Security;
using NSubstitute;

namespace LifeGrid.Infrastructure.Tests.Security;

public sealed class ApiCredentialSyncServiceTests
{
    private const string TokenKey = "Gemini_Provider_Token";

    // Known test vector: "AB" XOR 0xAB → bytes { 0xAB^0x41, 0xAB^0x42 } = { 0xEA, 0xE9 }
    private static readonly byte[] ObfuscatedAB = new byte[] { 0xEA, 0xE9 };
    private const byte XorSalt = 0xAB;

    private static (ApiCredentialSyncService sut, ISecureStorageService storage, IBuildSecretProvider provider)
        BuildSut()
    {
        var storage  = Substitute.For<ISecureStorageService>();
        var provider = Substitute.For<IBuildSecretProvider>();
        provider.GetObfuscatedBytes().Returns(ObfuscatedAB);
        provider.GetXorSalt().Returns(XorSalt);
        var sut = new ApiCredentialSyncService(storage, provider);
        return (sut, storage, provider);
    }

    [Fact]
    public async Task WhenTokenExists_DoesNotAccessBuildSecretProvider()
    {
        var (sut, storage, provider) = BuildSut();
        storage.GetAsync(TokenKey).Returns("existing-token");

        await sut.SyncAsync();

        provider.DidNotReceive().GetObfuscatedBytes();
    }

    [Fact]
    public async Task WhenTokenExists_DoesNotWriteToStorage()
    {
        var (sut, storage, _) = BuildSut();
        storage.GetAsync(TokenKey).Returns("existing-token");

        await sut.SyncAsync();

        await storage.DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task WhenTokenMissing_WritesDecodedTokenExactlyOnce()
    {
        var (sut, storage, _) = BuildSut();
        storage.GetAsync(TokenKey).Returns((string?)null);

        await sut.SyncAsync();

        await storage.Received(1).SetAsync(TokenKey, "AB");
    }

    [Fact]
    public async Task WhenTokenMissing_SecondCallIsIdempotent()
    {
        var (sut, storage, _) = BuildSut();
        storage.GetAsync(TokenKey).Returns((string?)null, "AB");

        await sut.SyncAsync(); // first call  → Case B → writes
        await sut.SyncAsync(); // second call → Case A → skips

        await storage.Received(1).SetAsync(Arg.Any<string>(), Arg.Any<string>());
    }
}
