using FluentAssertions;
using LifeGrid.Domain.UserProfile;
using LifeGrid.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using GoalAggregate = LifeGrid.Domain.Goal.Goal;

namespace LifeGrid.Infrastructure.Tests.Schema;

public sealed class GoalRefinementAnswerSchemaTests : IDisposable
{
    private readonly SqliteConnection  _connection;
    private readonly LifeGridDbContext _db;

    public GoalRefinementAnswerSchemaTests()
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

    private async Task<GoalAggregate> SeedGoalAsync(Guid userId)
    {
        var goal = GoalAggregate.Create(
            userId, "Run a marathon", "Physical", "6 months",
            new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DateTime.Now);
        _db.Goals.Add(goal);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return goal;
    }

    [Fact]
    public async Task GoalRefinementAnswers_TableExists_AfterMigration()
    {
        var profile = await SeedUserProfileAsync();
        var goal    = await SeedGoalAsync(profile.UserId);

        // Load goal and verify RefinementAnswers collection is queryable (proves table exists)
        var stored = await _db.Goals
            .Include("RefinementAnswers")
            .FirstAsync(g => g.GoalId == goal.GoalId);

        stored.RefinementAnswers.Should().BeEmpty();
    }

    [Fact]
    public async Task GoalRefinementAnswer_CanBeWrittenAndReadBack_WithFilledAnswer()
    {
        var profile = await SeedUserProfileAsync();
        var goal    = await SeedGoalAsync(profile.UserId);

        var stored = await _db.Goals.FindAsync(goal.GoalId);
        stored!.SetRefinementAnswers(new[] { (1, "What is your age?", (string?)"32") });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var reloaded = await _db.Goals
            .Include("RefinementAnswers")
            .FirstAsync(g => g.GoalId == goal.GoalId);

        reloaded.RefinementAnswers.Should().HaveCount(1);
        reloaded.RefinementAnswers.Single().RankOrder.Should().Be(1);
        reloaded.RefinementAnswers.Single().Question.Should().Be("What is your age?");
        reloaded.RefinementAnswers.Single().Answer.Should().Be("32");
    }

    [Fact]
    public async Task GoalRefinementAnswer_CanBeWrittenAndReadBack_WithNullAnswer()
    {
        var profile = await SeedUserProfileAsync();
        var goal    = await SeedGoalAsync(profile.UserId);

        var stored = await _db.Goals.FindAsync(goal.GoalId);
        stored!.SetRefinementAnswers(new[] { (1, "Any limitations?", (string?)null) });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var reloaded = await _db.Goals
            .Include("RefinementAnswers")
            .FirstAsync(g => g.GoalId == goal.GoalId);

        reloaded.RefinementAnswers.Single().Answer.Should().BeNull();
    }

    [Fact]
    public async Task OnboardingSession_StagingColumns_AreNullableAndPersist()
    {
        using var db2  = new LifeGridDbContext(new DbContextOptionsBuilder<LifeGridDbContext>()
            .UseSqlite(_connection).Options);

        var session = LifeGrid.Domain.Onboarding.OnboardingSession.Create();
        session.AdvanceToStep1();
        session.AdvanceToRefinementQuestionsActive("{\"test\":1}", "[{\"RankOrder\":1}]");
        db2.OnboardingSessions.Add(session);
        await db2.SaveChangesAsync();
        db2.ChangeTracker.Clear();

        var reloaded = await db2.OnboardingSessions.FindAsync(session.SessionId);

        reloaded.Should().NotBeNull();
        reloaded!.ValidatedGoalJson.Should().Be("{\"test\":1}");
        reloaded.RefinementQuestionsJson.Should().Be("[{\"RankOrder\":1}]");
    }
}
