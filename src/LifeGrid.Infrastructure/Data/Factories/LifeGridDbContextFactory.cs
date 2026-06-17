using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LifeGrid.Infrastructure.Data.Factories;

public sealed class LifeGridDbContextFactory : IDesignTimeDbContextFactory<LifeGridDbContext>
{
    public LifeGridDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LifeGridDbContext>()
            .UseSqlite("Data Source=lifegrid-dev.db")
            .Options;
        return new LifeGridDbContext(options);
    }
}
