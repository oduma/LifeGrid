using FluentAssertions;
using LifeGrid.Domain.Goal;
using LifeGrid.Domain.Onboarding;
using LifeGrid.Domain.UserProfile;
using LifeGrid.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LifeGrid.Infrastructure.Tests.Schema;

public sealed class GoalSchemaTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LifeGridDbContext _db;

    public GoalSchemaTests()
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

    private async Task<UserProfile> SeedUserProfileAsync()
    {
        var profile = UserProfile.Create();
        _db.UserProfiles.Add(profile);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return profile;
    }

    [Fact]
    public async Task Goal_CanBeWrittenAndReadBack()
    {
        var profile  = await SeedUserProfileAsync();
        var deadline = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var goal     = Goal.Create(profile.UserId, "Run a marathon", "Fitness", "6 months", deadline, DateTime.Now);

        _db.Goals.Add(goal);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var stored = await _db.Goals.FindAsync(goal.GoalId);

        stored.Should().NotBeNull();
        stored!.GoalId.Should().Be(goal.GoalId);
        stored.UserId.Should().Be(profile.UserId);
        stored.Status.Should().Be(GoalStatus.Active);
        stored.Description.Should().Be("Run a marathon");
    }

    [Fact]
    public async Task Goal_LinkedBadHabit_CanBeWrittenAndReadBack()
    {
        var profile  = await SeedUserProfileAsync();
        var deadline = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var goalId   = Guid.NewGuid();

        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Goals (GoalId, UserId, Description, AmbientTag, Duration, DeadlineDate, Status) VALUES ({0}, {1}, 'Test goal', 'Tag', '3 months', '2027-01-01', 'Active')",
            goalId, profile.UserId);

        var habitId = Guid.NewGuid();
        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO GoalLinkedBadHabits (BadHabitId, Description, DangerLevel, GoalId) VALUES ({0}, 'Snoozing alarm', 4, {1})",
            habitId, goalId);

        _db.ChangeTracker.Clear();
        var stored = await _db.Goals.FindAsync(goalId);

        stored.Should().NotBeNull();
        stored!.LinkedBadHabits.Should().HaveCount(1);
        stored.LinkedBadHabits.First().BadHabitId.Should().Be(habitId);
        stored.LinkedBadHabits.First().DangerLevel.Should().Be(4);
    }

    [Fact]
    public async Task OnboardingProgressCache_UserIdColumn_IsNullable()
    {
        // Existing OnboardingSession rows must survive the migration with UserId = NULL
        var session = OnboardingSession.Create();
        _db.OnboardingSessions.Add(session);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var stored = await _db.OnboardingSessions.FindAsync(session.SessionId);

        stored.Should().NotBeNull();
        stored!.UserId.Should().BeNull();
    }
}
