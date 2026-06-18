using FluentAssertions;
using LifeGrid.Domain.Goal;
using LifeGrid.Domain.UserProfile;
using LifeGrid.Infrastructure.Data;
using LifeGrid.Infrastructure.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WeekEntity     = LifeGrid.Domain.Week.Week;
using WeekGoalEntity = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Infrastructure.Tests.Data;

public sealed class WeekRepositoryTests : IDisposable
{
    private readonly SqliteConnection  _connection;
    private readonly LifeGridDbContext _db;

    public WeekRepositoryTests()
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

    private async Task<Guid> SeedGoalAsync()
    {
        var profile = UserProfile.Create();
        _db.UserProfiles.Add(profile);
        await _db.SaveChangesAsync();

        var goal = Goal.Create(
            profile.UserId, "Run a marathon", "Physical", "6 months",
            new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc));
        _db.Goals.Add(goal);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return goal.GoalId;
    }

    [Fact]
    public async Task AddAsync_PersistsWeekAndWeekGoalAfterCommit()
    {
        var goalId     = await SeedGoalAsync();
        var repository = new WeekRepository(_db);

        var week     = WeekEntity.Create(1, new DateTime(2026, 6, 16));
        var weekGoal = WeekGoalEntity.Create(week.WeekId, goalId);

        await repository.AddAsync(week, weekGoal);
        await _db.SaveChangesAsync(); // simulate UoW commit
        _db.ChangeTracker.Clear();

        (await _db.Weeks.CountAsync()).Should().Be(1);
        (await _db.WeekGoals.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AddAsync_WeekGoalLinkedToCorrectGoalId()
    {
        var goalId     = await SeedGoalAsync();
        var repository = new WeekRepository(_db);

        var week     = WeekEntity.Create(1, new DateTime(2026, 6, 16));
        var weekGoal = WeekGoalEntity.Create(week.WeekId, goalId);

        await repository.AddAsync(week, weekGoal);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var stored = await _db.WeekGoals.SingleAsync();
        stored.GoalId.Should().Be(goalId);
        stored.WeekId.Should().Be(week.WeekId);
    }

    [Fact]
    public async Task AddAsync_DoesNotCallSaveChangesItself()
    {
        var goalId     = await SeedGoalAsync();
        var repository = new WeekRepository(_db);

        var week     = WeekEntity.Create(1, new DateTime(2026, 6, 16));
        var weekGoal = WeekGoalEntity.Create(week.WeekId, goalId);

        await repository.AddAsync(week, weekGoal);
        // No commit — nothing should be in the DB yet
        _db.ChangeTracker.Clear();

        (await _db.Weeks.CountAsync()).Should().Be(0);
        (await _db.WeekGoals.CountAsync()).Should().Be(0);
    }
}
