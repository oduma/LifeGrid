using FluentAssertions;
using LifeGrid.Domain.Goal;
using LifeGrid.Domain.Onboarding;
using LifeGrid.Domain.UserProfile;
using LifeGrid.Infrastructure.Data;
using LifeGrid.Infrastructure.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using HabitEntity    = LifeGrid.Domain.Habit.Habit;
using WeekEntity     = LifeGrid.Domain.Week.Week;
using WeekGoalEntity = LifeGrid.Domain.WeekGoal.WeekGoal;

namespace LifeGrid.Infrastructure.Tests.Data;

public sealed class FactoryResetServiceTests : IDisposable
{
    private readonly SqliteConnection  _connection;
    private readonly LifeGridDbContext _db;

    public FactoryResetServiceTests()
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

    // ── seed helper ───────────────────────────────────────────────────────────

    private async Task SeedAllTablesAsync()
    {
        // UserProfile
        var profile = UserProfile.Create();
        _db.UserProfiles.Add(profile);
        await _db.SaveChangesAsync();

        // Goal
        var goal = Goal.Create(
            profile.UserId, "Run a marathon", "Physical", "6 months",
            new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 16));
        _db.Goals.Add(goal);
        await _db.SaveChangesAsync();

        // Week → WeekGoal → Habit
        var week     = WeekEntity.Create(1, new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc));
        var weekGoal = WeekGoalEntity.Create(week.WeekId, goal.GoalId, 1);
        _db.Weeks.Add(week);
        _db.WeekGoals.Add(weekGoal);
        await _db.SaveChangesAsync();

        var habit = HabitEntity.Create(
            weekGoal.WeekGoalId,
            "Morning run", "Run 5km",
            5.0, "km",
            new DateTime(2026, 6, 22, 8, 0, 0, DateTimeKind.Utc));
        _db.Habits.Add(habit);
        await _db.SaveChangesAsync();

        // OnboardingSession (completed)
        var session = OnboardingSession.Create();
        session.UpdateDraft("Run a marathon");
        session.AdvanceToStep1();
        session.LinkToUser(profile.UserId);
        session.AdvanceToAwaitingValidation();
        session.AdvanceToRefinementQuestionsActive(
            "{\"description\":\"Run\"}",
            "[{\"rankOrder\":1,\"question\":\"Baseline?\"}]");
        session.AdvanceToExecutionVerified();
        session.AdvanceToHabitsGenerated();
        _db.OnboardingSessions.Add(session);
        await _db.SaveChangesAsync();

        _db.ChangeTracker.Clear();
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WithAllTablesPopulated_LeavesZeroRowsInAllDomainTables()
    {
        await SeedAllTablesAsync();
        var service = new FactoryResetService(_db);

        await service.ResetAsync();

        (await _db.Habits.CountAsync()).Should().Be(0);
        (await _db.WeekGoals.CountAsync()).Should().Be(0);
        (await _db.Weeks.CountAsync()).Should().Be(0);
        (await _db.Goals.CountAsync()).Should().Be(0);
        (await _db.UserProfiles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ResetsOnboardingSessionToUnstarted()
    {
        await SeedAllTablesAsync();
        var service = new FactoryResetService(_db);

        await service.ResetAsync();
        _db.ChangeTracker.Clear();

        var session = await _db.OnboardingSessions.SingleAsync();
        session.CurrentStep.Should().Be(OnboardingStep.Unstarted);
        session.IsComplete.Should().BeFalse();
        session.UserId.Should().BeNull();
        session.RawGoalDraft.Should().BeNull();
        session.ValidatedGoalJson.Should().BeNull();
        session.RefinementQuestionsJson.Should().BeNull();
        session.RefinementAnswersJson.Should().BeNull();
    }
}
