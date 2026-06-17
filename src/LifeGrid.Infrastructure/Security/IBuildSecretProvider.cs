namespace LifeGrid.Infrastructure.Security;

internal interface IBuildSecretProvider
{
    byte[] GetObfuscatedBytes();
    byte GetXorSalt();
}
