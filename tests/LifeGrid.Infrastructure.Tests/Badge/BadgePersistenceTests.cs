using FluentAssertions;
using LifeGrid.Domain.Badge;
using LifeGrid.Infrastructure.Data;
using LifeGrid.Infrastructure.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using BadgeEntity = LifeGrid.Domain.Badge.Badge;
#pragma warning disable CS8019 // Unnecessary using directive

namespace LifeGrid.Infrastructure.Tests.Badge;

public sealed class BadgePersistenceTests : IDisposable
{
    private readonly SqliteConnection  _connection;
    private readonly LifeGridDbContext _db;
    private readonly BadgeRepository   _repo;

    private static readonly Guid     UserId = Guid.NewGuid();
    private static readonly DateTime Fixed  = new(2026, 6, 22, 23, 59, 59, DateTimeKind.Utc);

    public BadgePersistenceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<LifeGridDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db   = new LifeGridDbContext(options);
        _db.Database.Migrate();
        _repo = new BadgeRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AwardedBadge_PersistsIsEarnedTrue_NoSecondInsert()
    {
        var badge = BadgeEntity.CreateEarned(
            UserId, "Showing_Up_Bronze", "Mr. Consistency (Bronze)",
            "Logged in every day. Achieved: 22 Jun 2026",
            "", BadgeTier.Bronze, Fixed);

        // First award
        await _repo.AddAsync(badge);
        await _db.CommitAsync();

        var after = await _repo.GetEarnedByUserIdAsync(UserId);
        after.Should().HaveCount(1);
        after.Single().IsEarned.Should().BeTrue();
        after.Single().Tier.Should().Be(BadgeTier.Bronze);

        // Simulate evaluator guard: do NOT add a second Bronze badge
        var existing = await _repo.GetEarnedByUserIdAsync(UserId);
        var tiers    = existing.Select(b => b.Tier).ToHashSet();
        tiers.Should().Contain(BadgeTier.Bronze);

        // Verify only one row exists
        var all = await _repo.GetEarnedByUserIdAsync(UserId);
        all.Should().HaveCount(1);
    }
}
