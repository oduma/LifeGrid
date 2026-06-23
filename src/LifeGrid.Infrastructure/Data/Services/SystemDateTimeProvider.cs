using LifeGrid.Application.Common;

namespace LifeGrid.Infrastructure.Data.Services;

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
