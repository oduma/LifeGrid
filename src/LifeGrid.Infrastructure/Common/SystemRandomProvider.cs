using LifeGrid.Application.Common;

namespace LifeGrid.Infrastructure.Common;

internal sealed class SystemRandomProvider : IRandomProvider
{
    public int Next(int maxExclusive) => Random.Shared.Next(maxExclusive);
}
