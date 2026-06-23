using FluentAssertions;
using LifeGrid.Domain.Goal;
using LifeGrid.Domain.Habit;
using LifeGrid.Domain.UserProfile;
using LifeGrid.Infrastructure.Data;
using LifeGrid.Infrastructure.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using HabitEntity    = LifeGrid.Domain.Habit.Habit;
using WeekEntity     = LifeGrid.Domain.Week.Week;
using WeekGoalEntity = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Infrastructure.Tests.Data;

public sealed class HabitRepositoryTests : IDisposable
{
    private readonly SqliteConnection  _connection;
    private readonly LifeGridDbContext _db;

    public HabitRepositoryTests()
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

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<(Guid goalId, Guid weekGoalId)> SeedWeekGoalAsync()
    {
        var profile = UserProfile.Create();
        _db.UserProfiles.Add(profile);
        await _db.SaveChangesAsync();

        var goal = Goal.Create(profile.UserId, "Run a marathon", "Physical", "6 months",
            new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22),
            new DateTime(2026, 6, 16));
        _db.Goals.Add(goal);
        await _db.SaveChangesAsync();

        var week     = WeekEntity.Create(1, new DateTime(2026, 6, 16));
        var weekGoal = WeekGoalEntity.Create(week.WeekId, goal.GoalId, 1);
        _db.Weeks.Add(week);
        _db.WeekGoals.Add(weekGoal);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        return (goal.GoalId, weekGoal.WeekGoalId);
    }

    private static IReadOnlyList<HabitEntity> BuildHabits(Guid weekGoalId, int count = 3)
        => Enumerable.Range(1, count)
            .Select(i => HabitEntity.Create(
                weekGoalId, LifeGrid.Domain.Habit.HabitType.Planned,
                $"Run {i * 3} km", $"Run {i * 3} kilometres",
                i * 3.0, "km", new DateTime(2026, 6, 22)))
            .ToList();

    // ── AddRangeAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AddRangeAsync_PersistsHabitsAfterCommit()
    {
        var (_, weekGoalId) = await SeedWeekGoalAsync();
        var habits     = BuildHabits(weekGoalId, count: 3);
        var repository = new HabitRepository(_db);

        await repository.AddRangeAsync(habits);
        await _db.SaveChangesAsync(); // simulate UoW commit
        _db.ChangeTracker.Clear();

        (await _db.Habits.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task AddRangeAsync_DoesNotCallSaveChangesItself()
    {
        var (_, weekGoalId) = await SeedWeekGoalAsync();
        var habits     = BuildHabits(weekGoalId);
        var repository = new HabitRepository(_db);

        await repository.AddRangeAsync(habits);
        _db.ChangeTracker.Clear(); // no commit

        (await _db.Habits.CountAsync()).Should().Be(0);
    }

    // ── GetByWeekGoalIdAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetByWeekGoalIdAsync_ReturnsOnlyHabitsForThatWeekGoal()
    {
        var (goalId, weekGoalId1) = await SeedWeekGoalAsync();

        // Seed a second week/weekgoal
        var week2     = WeekEntity.Create(2, new DateTime(2026, 6, 23));
        var weekGoal2 = WeekGoalEntity.Create(week2.WeekId, goalId, 2);
        _db.Weeks.Add(week2);
        _db.WeekGoals.Add(weekGoal2);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var habitsForWeek1 = BuildHabits(weekGoalId1, count: 2);
        var habitsForWeek2 = BuildHabits(weekGoal2.WeekGoalId, count: 3);

        var repository = new HabitRepository(_db);
        await repository.AddRangeAsync(habitsForWeek1);
        await repository.AddRangeAsync(habitsForWeek2);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var result = await repository.GetByWeekGoalIdAsync(weekGoalId1);

        result.Should().HaveCount(2);
        result.All(h => h.WeekGoalId == weekGoalId1).Should().BeTrue();
    }

    [Fact]
    public async Task GetByWeekGoalIdAsync_ReturnsEmptyForUnknownId()
    {
        var repository = new HabitRepository(_db);

        var result = await repository.GetByWeekGoalIdAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    // ── AddCompletionLogAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task AddCompletionLogAsync_IncreasesCount()
    {
        var (_, weekGoalId) = await SeedWeekGoalAsync();
        var habits          = BuildHabits(weekGoalId, count: 1);
        var repository      = new HabitRepository(_db);

        await repository.AddRangeAsync(habits);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var log = CompletedValueLog.Create(
            habits[0].HabitId, 5.0, "km", null, null,
            new DateTime(2026, 6, 23, 10, 0, 0, DateTimeKind.Utc));

        await repository.AddCompletionLogAsync(log);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        (await _db.CompletedValueLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AddCompletionLogAsync_PersistsCorrectTimestamp()
    {
        var (_, weekGoalId) = await SeedWeekGoalAsync();
        var habits          = BuildHabits(weekGoalId, count: 1);
        var repository      = new HabitRepository(_db);

        await repository.AddRangeAsync(habits);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var expectedTimestamp = new DateTime(2026, 6, 23, 14, 30, 0, DateTimeKind.Utc);
        var log = CompletedValueLog.Create(
            habits[0].HabitId, 3.0, "km", "Good run", null, expectedTimestamp);

        await repository.AddCompletionLogAsync(log);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var persisted = await _db.CompletedValueLogs.FirstAsync();
        persisted.Timestamp.Should().Be(expectedTimestamp);
    }
}
