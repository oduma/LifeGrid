using FluentAssertions;
using LifeGrid.Domain.ViceCheck;
using LifeGrid.Infrastructure.Data;
using LifeGrid.Infrastructure.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LifeGrid.Infrastructure.Tests.Data;

public sealed class ViceCheckAuditRepositoryTests : IDisposable
{
    private readonly SqliteConnection  _connection;
    private readonly LifeGridDbContext _db;

    public ViceCheckAuditRepositoryTests()
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

    private static ViceCheckAudit BuildAudit(Guid weekId, Guid badHabitId, string question) =>
        ViceCheckAudit.Create(
            weekId, Guid.NewGuid(), badHabitId,
            "Get fit", "Late-night snacking", 5,
            question, new DateTime(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task AddAsync_PersistsAfterCommit()
    {
        var audit      = BuildAudit(Guid.NewGuid(), Guid.NewGuid(), "How's your evening routine?");
        var repository = new ViceCheckAuditRepository(_db);

        await repository.AddAsync(audit);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        (await _db.ViceCheckAudits.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task HasAuditForWeek_Exists_ReturnsTrue()
    {
        var weekId     = Guid.NewGuid();
        var audit      = BuildAudit(weekId, Guid.NewGuid(), "Question?");
        var repository = new ViceCheckAuditRepository(_db);
        await repository.AddAsync(audit);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        (await repository.HasAuditForWeekAsync(weekId)).Should().BeTrue();
    }

    [Fact]
    public async Task HasAuditForWeek_None_ReturnsFalse()
    {
        var repository = new ViceCheckAuditRepository(_db);

        (await repository.HasAuditForWeekAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task GetById_ReturnsCorrectAudit()
    {
        var audit      = BuildAudit(Guid.NewGuid(), Guid.NewGuid(), "Question?");
        var repository = new ViceCheckAuditRepository(_db);
        await repository.AddAsync(audit);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var result = await repository.GetByIdAsync(audit.AuditId);

        result.Should().NotBeNull();
        result!.AuditId.Should().Be(audit.AuditId);
    }

    [Fact]
    public async Task GetById_Unknown_ReturnsNull()
    {
        var repository = new ViceCheckAuditRepository(_db);

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPreviousQuestionsForBadHabit_ReturnsAllPriorQuestions_AnyStatus()
    {
        var badHabitId = Guid.NewGuid();
        var repository = new ViceCheckAuditRepository(_db);

        var passedAudit = BuildAudit(Guid.NewGuid(), badHabitId, "Did you snack at midnight?");
        passedAudit.MarkPassed("No, I went to bed.", new DateTime(2026, 6, 26, 8, 0, 0, DateTimeKind.Utc));
        var pendingAudit = BuildAudit(Guid.NewGuid(), badHabitId, "How was your fridge discipline?");

        await repository.AddAsync(passedAudit);
        await repository.AddAsync(pendingAudit);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var result = await repository.GetPreviousQuestionsForBadHabitAsync(badHabitId);

        result.Should().BeEquivalentTo(new[] { "Did you snack at midnight?", "How was your fridge discipline?" });
    }

    [Fact]
    public async Task GetPreviousQuestionsForBadHabit_NoPriorAudits_ReturnsEmpty()
    {
        var repository = new ViceCheckAuditRepository(_db);

        var result = await repository.GetPreviousQuestionsForBadHabitAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPreviousQuestionsForBadHabit_ScopedToBadHabitId_ExcludesOtherHabits()
    {
        var targetHabitId = Guid.NewGuid();
        var otherHabitId   = Guid.NewGuid();
        var repository     = new ViceCheckAuditRepository(_db);

        await repository.AddAsync(BuildAudit(Guid.NewGuid(), targetHabitId, "Question about target habit"));
        await repository.AddAsync(BuildAudit(Guid.NewGuid(), otherHabitId, "Question about other habit"));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var result = await repository.GetPreviousQuestionsForBadHabitAsync(targetHabitId);

        result.Should().ContainSingle().Which.Should().Be("Question about target habit");
    }
}
