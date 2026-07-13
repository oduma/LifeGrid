namespace LifeGrid.Application.Common;

public interface IRandomProvider
{
    int Next(int maxExclusive);
}
