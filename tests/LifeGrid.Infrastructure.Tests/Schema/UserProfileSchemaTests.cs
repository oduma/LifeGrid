using FluentAssertions;
using LifeGrid.Domain.UserProfile;
using LifeGrid.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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
    public async Task UserProfile_Badges_CanBeWrittenAndReadBack()
    {
        // Badges are awarded by the gamification engine; seed via EF directly to test schema
        var profileId = Guid.NewGuid();
        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO UserProfiles (UserId, CurrentLevel) VALUES ({0}, 1)", profileId);
        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO UserEconomy (UserProfileUserId, LifetimeGpAverage, LifetimeXp, CurrentSp, ShieldsAvailable, MaxShieldCap) VALUES ({0}, 0.0, 0, 0, 0, 2)", profileId);
        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO UserActiveStates (UserProfileUserId, DoubleXpMode, DoubleXpExpiry) VALUES ({0}, 0, '0001-01-01')", profileId);

        var badgeId = Guid.NewGuid();
        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO UserBadges (BadgeId, BadgeType, Description, DateEarned, UserId) VALUES ({0}, 'Showing_Up_Gold', 'First badge', '2026-06-17', {1})",
            badgeId, profileId);

        _db.ChangeTracker.Clear();
        var stored = await _db.UserProfiles.FindAsync(profileId);

        stored.Should().NotBeNull();
        stored!.Badges.Should().HaveCount(1);
        stored.Badges.First().BadgeId.Should().Be(badgeId);
        stored.Badges.First().BadgeType.Should().Be("Showing_Up_Gold");
    }
}
