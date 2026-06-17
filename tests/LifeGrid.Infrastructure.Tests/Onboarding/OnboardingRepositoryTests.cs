using FluentAssertions;
using LifeGrid.Domain.Onboarding;
using LifeGrid.Infrastructure.Data;
using LifeGrid.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LifeGrid.Infrastructure.Tests.Onboarding;

public sealed class OnboardingRepositoryTests : IDisposable
{
    private readonly LifeGridDbContext _db;
    private readonly OnboardingRepository _repository;

    public OnboardingRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<LifeGridDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db         = new LifeGridDbContext(options);
        _repository = new OnboardingRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetActiveSession_EmptyDb_ReturnsNull()
    {
        var result = await _repository.GetActiveSessionAsync();
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_NewSession_PersistsAndReturns()
    {
        var session = OnboardingSession.Create();

        var returned = await _repository.UpsertAsync(session);

        returned.Should().BeSameAs(session);
        var stored = await _db.OnboardingSessions.FindAsync(session.SessionId);
        stored.Should().NotBeNull();
        stored!.SessionId.Should().Be(session.SessionId);
    }

    [Fact]
    public async Task UpsertAsync_ExistingSession_UpdatesRecord()
    {
        var session = OnboardingSession.Create();
        await _repository.UpsertAsync(session);
        _db.ChangeTracker.Clear();

        session.UpdateDraft("updated goal");
        await _repository.UpsertAsync(session);
        _db.ChangeTracker.Clear();

        var stored = await _db.OnboardingSessions.FindAsync(session.SessionId);
        stored!.RawGoalDraft.Should().Be("updated goal");
    }

    [Fact]
    public async Task GetActiveSession_AfterInsert_ReturnsSession()
    {
        var session = OnboardingSession.Create();
        await _repository.UpsertAsync(session);
        _db.ChangeTracker.Clear();

        var result = await _repository.GetActiveSessionAsync();

        result.Should().NotBeNull();
        result!.SessionId.Should().Be(session.SessionId);
    }
}
