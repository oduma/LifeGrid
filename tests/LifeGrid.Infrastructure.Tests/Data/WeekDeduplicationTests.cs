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

public sealed class WeekDeduplicationTests : IDisposable
{
    private readonly SqliteConnection  _connection;
    private readonly LifeGridDbContext _db;

    public WeekDeduplicationTests()
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

    private async Task<(Guid goalIdA, Guid goalIdB)> SeedTwoGoalsAsync()
    {
        var profile = UserProfile.Create();
        _db.UserProfiles.Add(profile);
        await _db.SaveChangesAsync();

        var goalA = Goal.Create(profile.UserId, "Run a marathon",  "Fitness",  "6 months",
            new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 22), new DateTime(2026, 6, 16));
        var goalB = Goal.Create(profile.UserId, "Learn Spanish",   "Language", "12 months",
            new DateTime(2027,  6, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 22), new DateTime(2026, 6, 16));

        _db.Goals.AddRange(goalA, goalB);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        return (goalA.GoalId, goalB.GoalId);
    }

    [Fact]
    public async Task TwoGoalsSameStartDate_OnlyOneWeekEntityInDb()
    {
        var (goalIdA, goalIdB) = await SeedTwoGoalsAsync();
        var repository = new WeekRepository(_db);
        var sharedDate = new DateTime(2026, 6, 23);

        // GoalA: new week — uses AddAsync
        var week     = WeekEntity.Create(1, sharedDate);
        var weekGoalA = WeekGoalEntity.Create(week.WeekId, goalIdA, 1);
        await repository.AddAsync(week, weekGoalA);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // GoalB: week already exists — uses AddWeekGoalAsync with the same WeekId
        var existingWeek = await repository.GetByStartDateAsync(sharedDate);
        existingWeek.Should().NotBeNull();

        var weekGoalB = WeekGoalEntity.Create(existingWeek!.WeekId, goalIdB, 1);
        await repository.AddWeekGoalAsync(weekGoalB);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        (await _db.Weeks.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task TwoGoalsSameStartDate_BothWeekGoalsLinkToSameWeekId()
    {
        var (goalIdA, goalIdB) = await SeedTwoGoalsAsync();
        var repository = new WeekRepository(_db);
        var sharedDate = new DateTime(2026, 6, 23);

        var week      = WeekEntity.Create(1, sharedDate);
        var weekGoalA = WeekGoalEntity.Create(week.WeekId, goalIdA, 1);
        await repository.AddAsync(week, weekGoalA);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var existingWeek = await repository.GetByStartDateAsync(sharedDate);
        var weekGoalB    = WeekGoalEntity.Create(existingWeek!.WeekId, goalIdB, 1);
        await repository.AddWeekGoalAsync(weekGoalB);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var allWeekGoals = await _db.WeekGoals.ToListAsync();
        allWeekGoals.Should().HaveCount(2);
        allWeekGoals.Select(wg => wg.WeekId).Distinct().Should().HaveCount(1);
        allWeekGoals.Select(wg => wg.WeekId).Distinct().Single().Should().Be(week.WeekId);
    }

    [Fact]
    public async Task WeekGoalNumbers_AreSequentialPerGoal()
    {
        var (goalIdA, _) = await SeedTwoGoalsAsync();
        var repository = new WeekRepository(_db);

        // Goal A: two weeks with sequential WeekGoalNumbers
        var week1      = WeekEntity.Create(1, new DateTime(2026, 6, 23));
        var weekGoal1A = WeekGoalEntity.Create(week1.WeekId, goalIdA, 1);
        await repository.AddAsync(week1, weekGoal1A);

        var week2      = WeekEntity.Create(2, new DateTime(2026, 6, 30));
        var weekGoal2A = WeekGoalEntity.Create(week2.WeekId, goalIdA, 2);
        await repository.AddAsync(week2, weekGoal2A);

        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var goalAWeeks = await _db.WeekGoals
            .Where(wg => wg.GoalId == goalIdA)
            .OrderBy(wg => wg.WeekGoalNumber)
            .ToListAsync();

        goalAWeeks.Should().HaveCount(2);
        goalAWeeks[0].WeekGoalNumber.Should().Be(1);
        goalAWeeks[1].WeekGoalNumber.Should().Be(2);
    }
}
