using FluentAssertions;
using LifeGrid.Domain.Badge;
using LifeGrid.Domain.UserProfile;
using LifeGrid.Infrastructure.Data;
using LifeGrid.Infrastructure.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using BadgeEntity = LifeGrid.Domain.Badge.Badge;

namespace LifeGrid.Infrastructure.Tests.Schema;

public sealed class UserProfileSchemaTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LifeGridDbContext _db;

    public UserProfileSchemaTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<LifeGridDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new LifeGridDbContext(options);
        _db.Database.Migrate();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task UserProfile_CanBeWrittenAndReadBack()
    {
        var profile = UserProfile.Create();
        _db.UserProfiles.Add(profile);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var stored = await _db.UserProfiles.FindAsync(profile.UserId);

        stored.Should().NotBeNull();
        stored!.UserId.Should().Be(profile.UserId);
        stored.CurrentLevel.Should().Be(1);
    }

    [Fact]
    public async Task UserProfile_Economy_PersistedInSeparateTable()
    {
        var profile = UserProfile.Create();
        _db.UserProfiles.Add(profile);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var stored = await _db.UserProfiles.FindAsync(profile.UserId);

        stored.Should().NotBeNull();
        stored!.Economy.MaxShieldCap.Should().Be(2);
        stored.Economy.LifetimeXp.Should().Be(0);
        stored.Economy.CurrentSp.Should().Be(0);
    }

    [Fact]
    public async Task UserProfile_ActiveStates_PersistedInSeparateTable()
    {
        var profile = UserProfile.Create();
        _db.UserProfiles.Add(profile);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var stored = await _db.UserProfiles.FindAsync(profile.UserId);

        stored.Should().NotBeNull();
        stored!.ActiveStates.DoubleXpMode.Should().BeFalse();
        stored.ActiveStates.DoubleXpExpiry.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public async Task Badges_CanBeWrittenAndReadBack()
    {
        var profile = UserProfile.Create();
        _db.UserProfiles.Add(profile);
        await _db.SaveChangesAsync();

        var repo  = new BadgeRepository(_db);
        var badge = BadgeEntity.CreateEarned(
            profile.UserId, "Showing_Up_Bronze", "Mr. Consistency (Bronze)",
            "Logged in every day. Achieved: 22 Jun 2026",
            "", BadgeTier.Bronze,
            new DateTime(2026, 6, 22, 23, 59, 59, DateTimeKind.Utc));

        await repo.AddAsync(badge);
        await _db.CommitAsync();
        _db.ChangeTracker.Clear();

        var stored = await repo.GetEarnedByUserIdAsync(profile.UserId);

        stored.Should().HaveCount(1);
        stored.Single().BadgeType.Should().Be("Showing_Up_Bronze");
        stored.Single().Tier.Should().Be(BadgeTier.Bronze);
        stored.Single().IsEarned.Should().BeTrue();
    }
}
