using FluentAssertions;
using LifeGrid.Domain.Goal;
using LifeGrid.Domain.Habit;
using LifeGrid.Domain.UserProfile;
using LifeGrid.Infrastructure.Data;
using LifeGrid.Infrastructure.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WeekEntity     = LifeGrid.Domain.Week.Week;
using WeekGoalEntity = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Infrastructure.Tests.MomentBurst;

public sealed class MomentBurstHabitPersistenceTests : IDisposable
{
    private readonly SqliteConnection  _connection;
    private readonly LifeGridDbContext _db;
    private readonly HabitRepository   _repo;

    private static readonly DateTime Deadline = new(2026, 6, 28, 23, 59, 59, DateTimeKind.Utc);

    public MomentBurstHabitPersistenceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<LifeGridDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db   = new LifeGridDbContext(options);
        _db.Database.Migrate();
        _repo = new HabitRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<Guid> SeedWeekGoalAsync()
    {
        var profile = UserProfile.Create();
        _db.UserProfiles.Add(profile);
        await _db.CommitAsync();

        var goal = Goal.Create(
            profile.UserId, "Sprint training", "Physical", "4 weeks",
            new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        _db.Goals.Add(goal);
        await _db.CommitAsync();

        var week     = WeekEntity.Create(1, new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc));
        var weekGoal = WeekGoalEntity.Create(week.WeekId, goal.GoalId, 1);
        _db.Weeks.Add(week);
        _db.WeekGoals.Add(weekGoal);
        await _db.CommitAsync();
        _db.ChangeTracker.Clear();

        return weekGoal.WeekGoalId;
    }

    [Fact]
    public async Task MomentBurstHabit_PersistedWithCorrectHabitType()
    {
        var weekGoalId = await SeedWeekGoalAsync();

        var habit = Habit.Create(
            weekGoalId,
            HabitType.MomentBurst,
            "Quick Sprint",
            "Run 1km at tempo pace.",
            1.0,
            "km",
            Deadline);

        await _repo.AddRangeAsync([habit]);
        await _db.CommitAsync();
        _db.ChangeTracker.Clear();

        var stored = await _repo.GetByWeekGoalIdAsync(weekGoalId);

        stored.Should().HaveCount(1);
        stored[0].HabitType.Should().Be(HabitType.MomentBurst);
        stored[0].WeekGoalId.Should().Be(weekGoalId);
        stored[0].HabitName.Should().Be("Quick Sprint");
    }
}
