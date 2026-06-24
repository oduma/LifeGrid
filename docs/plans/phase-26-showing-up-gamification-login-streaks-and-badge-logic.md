# Phase 26 — Showing Up: Daily Login Streaks & Badge Logic
Status: DONE

---

## Pre-Flight Analysis (LEAD_ARCHITECT)

### Bounded Context: Badge / UserProfile / Presentation

Phase 26 spans three concerns:
1. **Login Tracking** — new `LoginHistory` aggregate (Domain → Infrastructure → App startup)
2. **Badge Awarding** — new standalone `Badge` entity (replaces Phase 25's `UserBadge` owned entity); `ConsistencyBadgeEvaluator` applies the weekly pattern algorithm from functional spec §3.10
3. **Presentation** — toast notification at startup + Vault tier-color update

### Breaking Changes from Phase 25

Phase 25 introduced `UserBadge` as an owned entity of `UserProfile` (stored in `UserBadges` table) and `UserProfile.AwardBadge(...)`. Phase 26 replaces this entirely with a standalone `Badge` entity. Because the `UserBadges` table has never held production data (Phase 25 was display-only; no awarding code shipped), the EF migration is a safe drop-and-recreate.

Files that change from Phase 25:
- `UserBadge.cs` — deleted
- `UserProfile.cs` — `_badges`, `Badges`, `AwardBadge` removed
- `UserProfileConfiguration.cs` — `OwnsMany(Badges)` block removed
- `BadgeDto.cs` — gains `BadgeName`, `Tier`, `IsEarned`, `DateEarned` (nullable)
- `GetUserBadgesQuery.cs` — rewritten to use `IBadgeRepository`
- `VaultBadgeItem.cs` — gains `TierColor` property
- `VaultPage.xaml` — icon `TextColor` binds to `TierColor`
- Tests for `UserBadge`, `UserProfile.AwardBadge`, `GetUserBadgesQuery` — rewritten/updated

### Architecture Decisions

- `ConsistencyBadgeEvaluator` lives in the **Application layer** (implements `IConsistencyBadgeEvaluator`). It uses only Application-layer repository interfaces — no Infrastructure dependency. This makes it fully unit-testable.
- `RecordLoginCommand` handler is in **Application layer**. It calls `ILoginHistoryRepository.AddAsync` then `IConsistencyBadgeEvaluator.EvaluateAsync` and returns the newly awarded `BadgeDto` list.
- `IToastNotificationService` is declared in **Application layer**. The Presentation layer provides `MauiToastNotificationService` (uses `DisplayAlert` for Phase 26; swappable later).
- The two DB writes (LoginHistory insert + Badge inserts) are split: the login row is committed first (its own `SaveChangesAsync`); badge inserts are committed together in a second `SaveChangesAsync`. This satisfies the "startup sequence atomicity" requirement: if badge evaluation or its DB write fails, the login record is still preserved.

### Risks

- Removing `UserProfile.Badges` invalidates the 3 `GetUserBadgesQueryTests` that seed via `profile.AwardBadge`. These are rewritten.
- `ConsistencyBadgeEvaluator` needs `IHabitRepository.HasCompletionLogsInRangeAsync` — a new method on an existing interface; the existing Infrastructure implementation needs updating.
- `VaultViewModel` is in `LifeGrid.Presentation` (net10.0-android); its tests remain deferred per established pattern.

---

## Clarification Decisions (captured)

| Question | Decision |
|----------|----------|
| Badge criteria | Weekly pattern (functional spec §3.10): logged in every day of a Mon–Sun week; tier determined by habit-log presence in Mon–Wed vs Thu–Sun |
| Caption text | `"Mr. Consistency (Bronze/Silver/Gold)"` per §3.10 names; date suffix `"Achieved: dd MMM yyyy"` |
| Badge storage | New standalone `Badges` table with FK to UserProfile; nullable FKs for future Goal/Week links; `IsEarned` flag; replaces Phase 25 `UserBadges` owned entity |
| Notification | Toast (implemented as `DisplayAlert` via `IToastNotificationService` abstraction; upgradeable) |

---

## Implementation Plan

### Phase 1 — Domain Layer (SENIOR_NET_DEVELOPER)

**Step 1.1 — `BadgeTier` enum**

File: `src/LifeGrid.Domain/Badge/BadgeTier.cs` (new)

```csharp
namespace LifeGrid.Domain.Badge;

public enum BadgeTier { Bronze, Silver, Gold }
```

**Step 1.2 — `Badge` entity**

File: `src/LifeGrid.Domain/Badge/Badge.cs` (new)

```csharp
namespace LifeGrid.Domain.Badge;

public sealed class Badge
{
    private Badge() { }

    public Guid      BadgeId     { get; private set; }
    public Guid      UserId      { get; private set; }
    public Guid?     GoalId      { get; private set; }
    public Guid?     WeekId      { get; private set; }
    public string    BadgeType   { get; private set; } = string.Empty;
    public string    BadgeName   { get; private set; } = string.Empty;
    public string    Description { get; private set; } = string.Empty;
    public string    IconName    { get; private set; } = string.Empty;
    public BadgeTier Tier        { get; private set; }
    public bool      IsEarned    { get; private set; }
    public DateTime? DateEarned  { get; private set; }

    public static Badge CreateEarned(
        Guid userId, string badgeType, string badgeName,
        string description, string iconName, BadgeTier tier, DateTime dateEarned)
        => new()
        {
            BadgeId     = Guid.NewGuid(),
            UserId      = userId,
            BadgeType   = badgeType,
            BadgeName   = badgeName,
            Description = description,
            IconName    = iconName,
            Tier        = tier,
            IsEarned    = true,
            DateEarned  = dateEarned
        };
}
```

**Step 1.3 — `LoginHistory` entity**

File: `src/LifeGrid.Domain/Badge/LoginHistory.cs` (new)

```csharp
namespace LifeGrid.Domain.Badge;

public sealed class LoginHistory
{
    private LoginHistory() { }

    public Guid     Id        { get; private set; }
    public Guid     UserId    { get; private set; }
    public DateTime Timestamp { get; private set; }

    public static LoginHistory Create(Guid userId, DateTime timestamp)
        => new() { Id = Guid.NewGuid(), UserId = userId, Timestamp = timestamp };
}
```

**Step 1.4 — Remove `UserBadge` and strip `UserProfile`**

- Delete `src/LifeGrid.Domain/UserProfile/UserBadge.cs`
- In `src/LifeGrid.Domain/UserProfile/UserProfile.cs`, remove:
  - `private readonly List<UserBadge> _badges = new();`
  - `public IReadOnlyCollection<UserBadge> Badges => _badges.AsReadOnly();`
  - `public void AwardBadge(string badgeType, string description, string iconName, DateTime dateEarned) ...`

---

### Phase 2 — Application Layer (SENIOR_NET_DEVELOPER)

**Step 2.1 — `IBadgeRepository`**

File: `src/LifeGrid.Application/Badge/IBadgeRepository.cs` (new)

```csharp
using BadgeEntity = LifeGrid.Domain.Badge.Badge;

namespace LifeGrid.Application.Badge;

public interface IBadgeRepository
{
    Task AddAsync(BadgeEntity badge, CancellationToken ct = default);
    Task<IReadOnlyList<BadgeEntity>> GetEarnedByUserIdAsync(Guid userId, CancellationToken ct = default);
}
```

**Step 2.2 — `ILoginHistoryRepository`**

File: `src/LifeGrid.Application/Badge/ILoginHistoryRepository.cs` (new)

```csharp
using LoginHistoryEntity = LifeGrid.Domain.Badge.LoginHistory;

namespace LifeGrid.Application.Badge;

public interface ILoginHistoryRepository
{
    Task AddAsync(LoginHistoryEntity entry, CancellationToken ct = default);
    Task<IReadOnlyList<DateTime>> GetTimestampsByUserIdAsync(Guid userId, CancellationToken ct = default);
}
```

**Step 2.3 — Update `BadgeDto`**

File: `src/LifeGrid.Application/Badge/BadgeDto.cs` (modify)

```csharp
using LifeGrid.Domain.Badge;

namespace LifeGrid.Application.Badge;

public record BadgeDto(
    Guid      BadgeId,
    string    BadgeType,
    string    BadgeName,
    string    IconName,
    string    Description,
    BadgeTier Tier,
    bool      IsEarned,
    DateTime? DateEarned);
```

**Step 2.4 — Update `GetUserBadgesQuery`**

File: `src/LifeGrid.Application/Badge/GetUserBadgesQuery.cs` (modify)

Rewrite the handler to use `IBadgeRepository` instead of `IUserProfileRepository`:

```csharp
public sealed class GetUserBadgesQueryHandler(IBadgeRepository badgeRepository, IUserProfileRepository userProfileRepository)
    : IRequestHandler<GetUserBadgesQuery, Result<IReadOnlyCollection<BadgeDto>>>
{
    public async Task<Result<IReadOnlyCollection<BadgeDto>>> Handle(
        GetUserBadgesQuery request, CancellationToken cancellationToken)
    {
        var profile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (profile is null)
            return Result<IReadOnlyCollection<BadgeDto>>.Success(Array.Empty<BadgeDto>());

        var badges = await badgeRepository.GetEarnedByUserIdAsync(profile.UserId, cancellationToken);
        if (!badges.Any())
            return Result<IReadOnlyCollection<BadgeDto>>.Success(Array.Empty<BadgeDto>());

        var dtos = badges
            .Select(b => new BadgeDto(b.BadgeId, b.BadgeType, b.BadgeName, b.IconName,
                                      b.Description, b.Tier, b.IsEarned, b.DateEarned))
            .ToArray();

        return Result<IReadOnlyCollection<BadgeDto>>.Success(dtos);
    }
}
```

**Step 2.5 — `IConsistencyBadgeEvaluator`**

File: `src/LifeGrid.Application/Badge/IConsistencyBadgeEvaluator.cs` (new)

```csharp
namespace LifeGrid.Application.Badge;

public interface IConsistencyBadgeEvaluator
{
    Task<IReadOnlyCollection<BadgeDto>> EvaluateAsync(Guid userId, CancellationToken ct = default);
}
```

**Step 2.6 — `ConsistencyBadgeEvaluator` (Application-layer implementation)**

File: `src/LifeGrid.Application/Badge/ConsistencyBadgeEvaluator.cs` (new)

The evaluator:
1. Loads all login timestamps for the user
2. Identifies complete Mon–Sun weeks where all 7 days have at least one login
3. For each such week queries habit completion log presence in Mon–Wed and Thu–Sun windows
4. Determines the tier per week (Bronze / Silver / Gold)
5. Skips tiers already earned
6. Creates and persists `Badge.CreateEarned(...)` for each new tier
7. Returns all newly created `BadgeDto` objects

```csharp
using LifeGrid.Application.Common;
using LifeGrid.Application.Habit;
using LifeGrid.Domain.Badge;
using BadgeEntity = LifeGrid.Domain.Badge.Badge;

namespace LifeGrid.Application.Badge;

public sealed class ConsistencyBadgeEvaluator(
    ILoginHistoryRepository loginHistoryRepository,
    IHabitRepository        habitRepository,
    IBadgeRepository        badgeRepository,
    IUnitOfWork             unitOfWork,
    IDateTimeProvider       clock)
    : IConsistencyBadgeEvaluator
{
    // event_available Unicode glyph for MaterialSymbolsRounded font
    private const string EventAvailableGlyph = "";

    public async Task<IReadOnlyCollection<BadgeDto>> EvaluateAsync(Guid userId, CancellationToken ct)
    {
        var timestamps = await loginHistoryRepository.GetTimestampsByUserIdAsync(userId, ct);
        if (timestamps.Count < 7)
            return Array.Empty<BadgeDto>();

        var earnedBadges = await badgeRepository.GetEarnedByUserIdAsync(userId, ct);
        var earnedTiers  = earnedBadges.Select(b => b.Tier).ToHashSet();

        if (earnedTiers.Count == 3)
            return Array.Empty<BadgeDto>(); // all tiers already awarded

        var loginDays = timestamps.Select(t => t.Date).ToHashSet();
        var allMondays = GetCompletedWeekMondays(loginDays, clock.UtcNow.Date);

        var newBadges = new List<BadgeEntity>();

        foreach (var monday in allMondays)
        {
            if (earnedTiers.Count == 3) break;

            var weekDays = Enumerable.Range(0, 7).Select(i => monday.AddDays(i)).ToList();
            if (!weekDays.All(d => loginDays.Contains(d))) continue;

            var thursday = monday.AddDays(3);
            var sunday   = monday.AddDays(6);

            bool hasMonWed = await habitRepository.HasCompletionLogsInRangeAsync(
                monday.ToUtcStart(), thursday.ToUtcStart(), ct);            // [Mon, Thu)
            bool hasThuSun = await habitRepository.HasCompletionLogsInRangeAsync(
                thursday.ToUtcStart(), sunday.AddDays(1).ToUtcStart(), ct); // [Thu, next Mon)

            var tier = (hasMonWed, hasThuSun) switch
            {
                (true,  _)    => BadgeTier.Gold,
                (false, true) => BadgeTier.Silver,
                _             => BadgeTier.Bronze
            };

            if (earnedTiers.Contains(tier)) continue;

            var dateEarned = sunday.ToUtcEndOfDay();
            var badge      = tier switch
            {
                BadgeTier.Gold   => BadgeEntity.CreateEarned(userId, "Showing_Up_Gold",
                    "Mr. Consistency (Gold)",
                    $"Logged in every day, first-half habits done. Achieved: {dateEarned:dd MMM yyyy}",
                    EventAvailableGlyph, BadgeTier.Gold, dateEarned),
                BadgeTier.Silver => BadgeEntity.CreateEarned(userId, "Showing_Up_Silver",
                    "Mr. Consistency (Silver)",
                    $"Logged in every day, habits on schedule. Achieved: {dateEarned:dd MMM yyyy}",
                    EventAvailableGlyph, BadgeTier.Silver, dateEarned),
                _                => BadgeEntity.CreateEarned(userId, "Showing_Up_Bronze",
                    "Mr. Consistency (Bronze)",
                    $"Logged in every day. Achieved: {dateEarned:dd MMM yyyy}",
                    EventAvailableGlyph, BadgeTier.Bronze, dateEarned)
            };

            await badgeRepository.AddAsync(badge, ct);
            earnedTiers.Add(tier);
            newBadges.Add(badge);
        }

        if (newBadges.Any())
            await unitOfWork.SaveChangesAsync(ct);

        return newBadges
            .Select(b => new BadgeDto(b.BadgeId, b.BadgeType, b.BadgeName, b.IconName,
                                      b.Description!, b.Tier, b.IsEarned, b.DateEarned))
            .ToArray();
    }

    private static IEnumerable<DateTime> GetCompletedWeekMondays(HashSet<DateTime> loginDays, DateTime today)
    {
        if (loginDays.Count == 0) return Enumerable.Empty<DateTime>();
        var first  = loginDays.Min();
        var monday = first.AddDays(-(int)first.DayOfWeek == 0 ? 6 : (int)first.DayOfWeek - 1);
        var result = new List<DateTime>();
        while (monday.AddDays(6) < today)
        {
            result.Add(monday);
            monday = monday.AddDays(7);
        }
        return result;
    }
}
```

Note: `ToUtcStart()` and `ToUtcEndOfDay()` are `DateTime` extension methods added in this step:

File: `src/LifeGrid.Application/Common/DateTimeExtensions.cs` (new)

```csharp
namespace LifeGrid.Application.Common;

public static class DateTimeExtensions
{
    public static DateTime ToUtcStart(this DateTime date)
        => DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

    public static DateTime ToUtcEndOfDay(this DateTime date)
        => DateTime.SpecifyKind(date.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
}
```

**Step 2.7 — `RecordLoginCommand`**

File: `src/LifeGrid.Application/Badge/RecordLoginCommand.cs` (new)

```csharp
using LifeGrid.Application.Common;
using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Badge;
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Badge;

public record RecordLoginCommand : IRequest<Result<IReadOnlyCollection<BadgeDto>>>;

public sealed class RecordLoginCommandHandler(
    IUserProfileRepository    userProfileRepository,
    ILoginHistoryRepository   loginHistoryRepository,
    IConsistencyBadgeEvaluator evaluator,
    IUnitOfWork               unitOfWork,
    IDateTimeProvider         clock)
    : IRequestHandler<RecordLoginCommand, Result<IReadOnlyCollection<BadgeDto>>>
{
    public async Task<Result<IReadOnlyCollection<BadgeDto>>> Handle(
        RecordLoginCommand request, CancellationToken cancellationToken)
    {
        var profile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (profile is null)
            return Result<IReadOnlyCollection<BadgeDto>>.Success(Array.Empty<BadgeDto>());

        var entry = LoginHistory.Create(profile.UserId, clock.UtcNow);
        await loginHistoryRepository.AddAsync(entry, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken); // commit login record first

        var newBadges = await evaluator.EvaluateAsync(profile.UserId, cancellationToken);
        return Result<IReadOnlyCollection<BadgeDto>>.Success(newBadges);
    }
}
```

**Step 2.8 — `IToastNotificationService`**

File: `src/LifeGrid.Application/Common/IToastNotificationService.cs` (new)

```csharp
namespace LifeGrid.Application.Common;

public interface IToastNotificationService
{
    Task ShowBadgesEarnedAsync(IReadOnlyCollection<BadgeDto> badges, CancellationToken ct = default);
}
```

Note: `BadgeDto` is in `LifeGrid.Application.Badge` — add `using LifeGrid.Application.Badge;`.

**Step 2.9 — Extend `IHabitRepository`**

File: `src/LifeGrid.Application/Habit/IHabitRepository.cs` (modify)

Add at the end of the interface:

```csharp
Task<bool> HasCompletionLogsInRangeAsync(DateTime startUtcInclusive, DateTime endUtcExclusive, CancellationToken ct = default);
```

---

### Phase 3 — Infrastructure Layer (SENIOR_NET_DEVELOPER)

**Step 3.1 — Remove `OwnsMany(Badges)` from `UserProfileConfiguration`**

File: `src/LifeGrid.Infrastructure/Data/EntityConfigurations/UserProfileConfiguration.cs` (modify)

Remove the entire `builder.OwnsMany(e => e.Badges, badge => { ... });` block.

**Step 3.2 — `BadgeConfiguration`**

File: `src/LifeGrid.Infrastructure/Data/EntityConfigurations/BadgeConfiguration.cs` (new)

```csharp
using LifeGrid.Domain.Badge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeGrid.Infrastructure.Data.EntityConfigurations;

public sealed class BadgeConfiguration : IEntityTypeConfiguration<Badge>
{
    public void Configure(EntityTypeBuilder<Badge> builder)
    {
        builder.ToTable("Badges");
        builder.HasKey(b => b.BadgeId);
        builder.Property(b => b.BadgeId).ValueGeneratedNever();
        builder.Property(b => b.UserId);
        builder.Property(b => b.GoalId);
        builder.Property(b => b.WeekId);
        builder.Property(b => b.BadgeType).HasMaxLength(100);
        builder.Property(b => b.BadgeName).HasMaxLength(200);
        builder.Property(b => b.Description).HasMaxLength(500);
        builder.Property(b => b.IconName).HasMaxLength(200);
        builder.Property(b => b.Tier).HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.IsEarned);
        builder.Property(b => b.DateEarned);
    }
}
```

**Step 3.3 — `LoginHistoryConfiguration`**

File: `src/LifeGrid.Infrastructure/Data/EntityConfigurations/LoginHistoryConfiguration.cs` (new)

```csharp
using LifeGrid.Domain.Badge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeGrid.Infrastructure.Data.EntityConfigurations;

public sealed class LoginHistoryConfiguration : IEntityTypeConfiguration<LoginHistory>
{
    public void Configure(EntityTypeBuilder<LoginHistory> builder)
    {
        builder.ToTable("LoginHistory");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.UserId);
        builder.Property(l => l.Timestamp);
    }
}
```

**Step 3.4 — Register `Badge` and `LoginHistory` on `LifeGridDbContext`**

File: `src/LifeGrid.Infrastructure/Data/LifeGridDbContext.cs` (modify)

Add:

```csharp
public DbSet<Badge>        Badges        => Set<Badge>();
public DbSet<LoginHistory> LoginHistory  => Set<LoginHistory>();
```

**Step 3.5 — `BadgeRepository`**

File: `src/LifeGrid.Infrastructure/Data/Repositories/BadgeRepository.cs` (new)

```csharp
using LifeGrid.Application.Badge;
using LifeGrid.Domain.Badge;
using Microsoft.EntityFrameworkCore;
using BadgeEntity = LifeGrid.Domain.Badge.Badge;

namespace LifeGrid.Infrastructure.Data.Repositories;

internal sealed class BadgeRepository(LifeGridDbContext db) : IBadgeRepository
{
    public Task AddAsync(BadgeEntity badge, CancellationToken ct = default)
    {
        db.Badges.Add(badge);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<BadgeEntity>> GetEarnedByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await db.Badges
            .Where(b => b.UserId == userId && b.IsEarned)
            .ToListAsync(ct);
}
```

**Step 3.6 — `LoginHistoryRepository`**

File: `src/LifeGrid.Infrastructure/Data/Repositories/LoginHistoryRepository.cs` (new)

```csharp
using LifeGrid.Application.Badge;
using LifeGrid.Domain.Badge;
using Microsoft.EntityFrameworkCore;
using LoginHistoryEntity = LifeGrid.Domain.Badge.LoginHistory;

namespace LifeGrid.Infrastructure.Data.Repositories;

internal sealed class LoginHistoryRepository(LifeGridDbContext db) : ILoginHistoryRepository
{
    public Task AddAsync(LoginHistoryEntity entry, CancellationToken ct = default)
    {
        db.LoginHistory.Add(entry);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<DateTime>> GetTimestampsByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await db.LoginHistory
            .Where(l => l.UserId == userId)
            .Select(l => l.Timestamp)
            .ToListAsync(ct);
}
```

**Step 3.7 — Implement `HasCompletionLogsInRangeAsync` on `HabitRepository`**

File: `src/LifeGrid.Infrastructure/Data/Repositories/HabitRepository.cs` (modify)

Add:

```csharp
public async Task<bool> HasCompletionLogsInRangeAsync(
    DateTime startUtcInclusive, DateTime endUtcExclusive, CancellationToken ct = default)
    => await db.Set<CompletedValueLog>()
        .AnyAsync(log => log.Timestamp >= startUtcInclusive && log.Timestamp < endUtcExclusive, ct);
```

**Step 3.8 — Update `FactoryResetService`**

File: `src/LifeGrid.Infrastructure/Data/Services/FactoryResetService.cs` (modify)

Add before the `UserProfiles` delete (FK-safe order — Badges and LoginHistory reference UserProfiles):

```csharp
await db.Database.ExecuteSqlRawAsync("DELETE FROM Badges",       ct);
await db.Database.ExecuteSqlRawAsync("DELETE FROM LoginHistory", ct);
```

**Step 3.9 — Register `ConsistencyBadgeEvaluator` in Infrastructure DI**

Note: `ConsistencyBadgeEvaluator` is in the Application layer and injected with Application-layer interfaces. Register it in `MauiProgram.cs` (see Phase 4).

**Step 3.10 — EF Migration**

```
dotnet ef migrations add Phase26_BadgesAndLoginHistory --project src/LifeGrid.Infrastructure
```

Expected changes:
- Drop table `UserBadges`
- Create table `Badges` (BadgeId, UserId, GoalId, WeekId, BadgeType, BadgeName, Description, IconName, Tier, IsEarned, DateEarned)
- Create table `LoginHistory` (Id, UserId, Timestamp)

---

### Phase 4 — Presentation Layer (MAUI_UX_ENGINEER)

**Step 4.1 — `MauiToastNotificationService`**

File: `src/LifeGrid.Presentation/Services/MauiToastNotificationService.cs` (new)

```csharp
using LifeGrid.Application.Badge;
using LifeGrid.Application.Common;

namespace LifeGrid.Presentation.Services;

internal sealed class MauiToastNotificationService : IToastNotificationService
{
    public async Task ShowBadgesEarnedAsync(IReadOnlyCollection<BadgeDto> badges, CancellationToken ct = default)
    {
        foreach (var badge in badges)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Application.Current!.MainPage!.DisplayAlert(
                    "Badge Unlocked!",
                    $"{badge.BadgeName} — {badge.Description}",
                    "Nice!"));
        }
    }
}
```

**Step 4.2 — Update `App.xaml.cs`**

File: `src/LifeGrid.Presentation/App.xaml.cs` (modify)

Inject `IToastNotificationService` and call `RecordLoginCommand` in `OnStart()`:

Add `IToastNotificationService _toastService` to constructor parameters.

After `GetOrCreateUserProfileQuery()` resolves and before the returning-user / new-user branching:

```csharp
var loginResult = await _mediator.Send(new RecordLoginCommand());
```

After `Shell.Current.GoToAsync("//home")` returns (both in the returning-user and new-user paths), add:

```csharp
if (loginResult.IsSuccess && loginResult.Value?.Any() == true)
    await _toastService.ShowBadgesEarnedAsync(loginResult.Value);
```

Full updated `OnStart()`:

```csharp
protected override async void OnStart()
{
    base.OnStart();
    await _credentialSync.SyncAsync();
    await _mediator.Send(new GetOrCreateUserProfileQuery());

    var loginResult = await _mediator.Send(new RecordLoginCommand());

    var countResult = await _mediator.Send(new GetActiveGoalCountQuery());
    if (countResult.IsSuccess && countResult.Value > 0)
    {
        _appShellViewModel.SetOnboardingComplete();
        await _hudViewModel.LoadAsync();
        await Shell.Current.GoToAsync("//home");
        if (loginResult.IsSuccess && loginResult.Value?.Any() == true)
            await _toastService.ShowBadgesEarnedAsync(loginResult.Value);
        return;
    }

    await _hudViewModel.LoadAsync();
    var sessionResult = await _mediator.Send(new GetOrCreateOnboardingSessionQuery());
    if (sessionResult.IsSuccess)
        await Shell.Current.GoToAsync("create-goal");

    if (loginResult.IsSuccess && loginResult.Value?.Any() == true)
        await _toastService.ShowBadgesEarnedAsync(loginResult.Value);
}
```

**Step 4.3 — Update `VaultBadgeItem`**

File: `src/LifeGrid.Presentation/ViewModels/VaultBadgeItem.cs` (modify)

Add `TierColor` property:

```csharp
public Color TierColor { get; init; } = Colors.White;
```

**Step 4.4 — Update `VaultViewModel.LoadAsync()`**

File: `src/LifeGrid.Presentation/ViewModels/VaultViewModel.cs` (modify)

Update the `BadgeDto` → `VaultBadgeItem` mapping to include tier color and use new `BadgeDto` fields:

```csharp
Badges.Add(new VaultBadgeItem
{
    IconGlyph   = dto.IconName,
    Title       = dto.BadgeName,
    Description = dto.Description,
    DateEarned  = dto.DateEarned ?? DateTime.MinValue,
    TierColor   = dto.Tier switch
    {
        BadgeTier.Gold   => Color.FromArgb("#FFC300"),
        BadgeTier.Silver => Color.FromArgb("#9CA3AF"),
        _                => Color.FromArgb("#D47A43")
    }
});
```

Add `using LifeGrid.Domain.Badge;` to `VaultViewModel.cs`.

**Step 4.5 — Update `VaultPage.xaml`**

File: `src/LifeGrid.Presentation/Pages/VaultPage.xaml` (modify)

Change the badge icon label's `TextColor` binding from the static resource to the view-model property:

```xml
<!-- Before -->
TextColor="{StaticResource Primary}"

<!-- After -->
TextColor="{Binding TierColor}"
```

**Step 4.6 — DI Registrations (`MauiProgram.cs`)**

File: `src/LifeGrid.Presentation/MauiProgram.cs` (modify)

Add after existing registrations:

```csharp
builder.Services.AddSingleton<IToastNotificationService, MauiToastNotificationService>();
builder.Services.AddScoped<IBadgeRepository, BadgeRepository>();
builder.Services.AddScoped<ILoginHistoryRepository, LoginHistoryRepository>();
builder.Services.AddScoped<IConsistencyBadgeEvaluator, ConsistencyBadgeEvaluator>();
```

---

### Phase 5 — Tests (TDD_SPECIALIST)

**Step 5.1 — Domain: Remove `UserBadgeTests.cs`**

Delete `tests/LifeGrid.Domain.Tests/UserProfile/UserBadgeTests.cs` (-1 test).

**Step 5.2 — Domain: Update `UserProfileTests.cs`**

Remove the two tests that depend on `UserProfile.Badges` and `AwardBadge`:
- `Create_HasEmptyBadgesCollection`
- `AwardBadge_AppendsToBadgesCollection`

(-2 domain tests)

**Step 5.3 — Domain: `BadgeTests.cs`** (new)

File: `tests/LifeGrid.Domain.Tests/Badge/BadgeTests.cs`

```
CreateEarned_SetsAllProperties           (+1)
CreateEarned_IsEarned_IsTrue             (+1)
```

**Step 5.4 — Domain: `LoginHistoryTests.cs`** (new)

File: `tests/LifeGrid.Domain.Tests/Badge/LoginHistoryTests.cs`

```
Create_SetsUserIdAndTimestamp            (+1)
```

Domain net: −3 + 3 = **0** (remains at 97)

**Step 5.5 — Application: Rewrite `GetUserBadgesQueryTests.cs`**

File: `tests/LifeGrid.Application.Tests/Badge/GetUserBadgesQueryTests.cs` (rewrite)

Use `IBadgeRepository` mock instead of `IUserProfileRepository.Badges`. Same 3 test names, new implementation:

```
NullProfile_ReturnsEmptyCollection
ProfileWithNoBadges_ReturnsEmptyCollection
ProfileWithBadges_ReturnsMappedDtos
```

(0 net change for Application)

**Step 5.6 — Application: `ConsistencyBadgeEvaluatorTests.cs`** (new)

File: `tests/LifeGrid.Application.Tests/Badge/ConsistencyBadgeEvaluatorTests.cs`

```
NoLogins_ReturnsEmpty                                          (+1)
FewerThanSevenDaysInAnyWeek_ReturnsEmpty                       (+1)
AllSevenDays_NoHabits_AwardsBronze                             (+1)
AllSevenDays_HabitsOnlyThuSun_AwardsSilver                     (+1)
AllSevenDays_HabitsIncludeMonWed_AwardsGold                    (+1)
TwoQualifyingWeeks_BronzeAndSilver_AwardsBoth                  (+1)
BronzeAlreadyEarned_QualifyingWeek_DoesNotDuplicate            (+1)
AllTiersAlreadyEarned_ReturnsEmpty                             (+1)
```

(+8 Application tests)

**Step 5.7 — Application: `RecordLoginCommandTests.cs`** (new)

File: `tests/LifeGrid.Application.Tests/Badge/RecordLoginCommandTests.cs`

```
NullProfile_ReturnsSuccessWithEmptyBadges                      (+1)
ValidProfile_InsertsLoginHistoryEntry                          (+1)
ValidProfile_EvaluatorResultIsReturned                         (+1)
```

(+3 Application tests)

**Step 5.8 — Infrastructure: Persistence Idempotency Integration Test** (new)

File: `tests/LifeGrid.Infrastructure.Tests/Badge/BadgePersistenceTests.cs`

```
AwardedBadge_PersistsIsEarnedTrue_NoSecondInsert               (+1)
```

Uses in-memory SQLite (same pattern as existing Infrastructure tests).

(+1 Infrastructure test)

**Step 5.9 — Presentation stub (update)**

File: `tests/LifeGrid.Application.Tests/Vault/VaultViewModelTests.cs` (update comment)

Update the stub comment to note the `TierColor` mapping deferral.

---

## Test Count Summary

| Project | Before | Removed | New | After |
|---------|--------|---------|-----|-------|
| LifeGrid.Domain.Tests | 97 | −3 | +3 | **97** |
| LifeGrid.Application.Tests | 140 | 0 | +11 | **151** |
| LifeGrid.Infrastructure.Tests | 66 | 0 | +1 | **67** |
| **Total** | **303** | **−3** | **+15** | **315** |

---

## Implementation Notes (Post-Approval Corrections)

1. **`IUnitOfWork` method:** Plan used `unitOfWork.SaveChangesAsync()`; actual interface exposes `CommitAsync()`. Corrected in `ConsistencyBadgeEvaluator` and `RecordLoginCommandHandler`.
2. **DI registration location:** `IBadgeRepository` / `ILoginHistoryRepository` moved from `MauiProgram.cs` to `InfrastructureServiceExtensions.AddInfrastructure()` because implementations are `internal`.
3. **`Application.Current` collision:** Added `using MauiApplication = Microsoft.Maui.Controls.Application;` alias in `MauiToastNotificationService` to resolve namespace ambiguity with `LifeGrid.Application`.
4. **Test Monday date:** `Monday = June 16` was a Tuesday; corrected to `Monday = June 15` (actual Monday). `week2Monday` corrected from June 9 to June 8.
5. **Badge type alias:** `using BadgeEntity = LifeGrid.Domain.Badge.Badge;` required in test files whose namespace contains `*.Badge` to avoid shadowing.
6. **`UserProfileSchemaTests`:** `UserProfile_Badges_CanBeWrittenAndReadBack` (testing old owned entity) replaced with `Badges_CanBeWrittenAndReadBack` (testing new `BadgeRepository`/`Badges` table).
7. **Test count:** Final totals — Domain: 97, Application: 151, Infrastructure: 67 → **315 total** (not 313 as stated in acceptance criteria; plan table was correct, acceptance-criteria text had a typo).
8. **Warning count:** Actual build produces **9 warnings** (improved from baseline 12); Phase 26 introduced zero new warnings.

---

## Original Implementation Notes

- The `GetCompletedWeekMondays` helper computes the ISO Monday of the earliest login week and iterates forward one week at a time, stopping before the current week (only complete weeks are evaluated).
- `IUnitOfWork.CommitAsync` is called twice in `RecordLoginCommandHandler`: once after inserting the `LoginHistory` row (guaranteeing the login is recorded even if evaluation fails), and once inside `ConsistencyBadgeEvaluator` after all badge inserts (atomic multi-badge commit).
- The `event_available` glyph `` is the correct Unicode code point for MaterialSymbolsRounded. Confirm during implementation against the font's cmap.
- `MauiToastNotificationService` uses `MainThread.InvokeOnMainThreadAsync` to ensure `DisplayAlert` is called on the UI thread regardless of the calling context.
