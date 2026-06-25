using FluentAssertions;
using LifeGrid.Domain.Notification;
using LifeGrid.Infrastructure.Data;
using LifeGrid.Infrastructure.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NotificationEntity = LifeGrid.Domain.Notification.Notification;

namespace LifeGrid.Infrastructure.Tests.Notification;

public sealed class NotificationRepositoryTests : IDisposable
{
    private readonly SqliteConnection       _connection;
    private readonly LifeGridDbContext      _db;
    private readonly NotificationRepository _repo;

    private static readonly DateTime Fixed = new(2026, 6, 25, 10, 0, 0, DateTimeKind.Utc);

    public NotificationRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<LifeGridDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db   = new LifeGridDbContext(options);
        _db.Database.Migrate();
        _repo = new NotificationRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // TDD invariant 1 — Persistence Verification
    [Fact]
    public async Task AddAsync_Commit_PersistsToDatabase_WithCorrectTimestamp()
    {
        var notification = NotificationEntity.Create(
            "Shield Earned", "You earned a shield!", NotificationType.ShieldUpdate,
            null, Fixed);

        await _repo.AddAsync(notification);
        await _db.CommitAsync();

        var all = await _repo.GetAllAsync();
        all.Should().HaveCount(1);
        all.Single().NotificationId.Should().Be(notification.NotificationId);
        all.Single().Title.Should().Be("Shield Earned");
        all.Single().Timestamp.Should().Be(Fixed);
        all.Single().IsRead.Should().BeFalse();
    }

    // TDD invariant 3 — Badge Count Logic
    [Fact]
    public async Task GetUnreadCountAsync_DecreasesAfterMarkAsRead()
    {
        var n1 = NotificationEntity.Create("A", "M", NotificationType.Warning, null, Fixed);
        var n2 = NotificationEntity.Create("B", "M", NotificationType.Quest,   null, Fixed.AddHours(-1));

        await _repo.AddAsync(n1);
        await _repo.AddAsync(n2);
        await _db.CommitAsync();

        var before = await _repo.GetUnreadCountAsync();
        before.Should().Be(2);

        var fetched = await _repo.GetByIdAsync(n1.NotificationId);
        fetched!.MarkRead();
        await _db.CommitAsync();

        var after = await _repo.GetUnreadCountAsync();
        after.Should().Be(1);
    }

    // TDD invariant 4 — Integrity Test
    [Fact]
    public async Task MarkAsRead_DoesNotModifyMessageOrTimestamp()
    {
        const string originalMessage = "Original message content.";
        var n = NotificationEntity.Create(
            "Title", originalMessage, NotificationType.WeeklyRecap,
            "lifegrid://goal/some-id", Fixed);

        await _repo.AddAsync(n);
        await _db.CommitAsync();

        var fetched = await _repo.GetByIdAsync(n.NotificationId);
        fetched!.MarkRead();
        await _db.CommitAsync();

        var afterRead = await _repo.GetByIdAsync(n.NotificationId);
        afterRead!.IsRead.Should().BeTrue();
        afterRead.Message.Should().Be(originalMessage);
        afterRead.Timestamp.Should().Be(Fixed);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsDescendingByTimestamp()
    {
        var older  = NotificationEntity.Create("Older",  "M", NotificationType.Quest, null, Fixed.AddHours(-2));
        var newer  = NotificationEntity.Create("Newer",  "M", NotificationType.Quest, null, Fixed);
        var middle = NotificationEntity.Create("Middle", "M", NotificationType.Quest, null, Fixed.AddHours(-1));

        await _repo.AddAsync(older);
        await _repo.AddAsync(newer);
        await _repo.AddAsync(middle);
        await _db.CommitAsync();

        var all = await _repo.GetAllAsync();
        all.Select(n => n.Title).Should().ContainInOrder("Newer", "Middle", "Older");
    }
}
