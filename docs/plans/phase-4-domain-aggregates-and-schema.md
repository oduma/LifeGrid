# Implementation Plan: Phase 4 — Production Schema & Domain Aggregate Initializer

**Status:** DONE — completed 2026-06-17
**Requirements Source:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` (P4.1–P4.13)
**Target:** Domain + Application + Infrastructure + Presentation (no new packages)

---

## Key Decisions (from clarifications)

| Decision | Choice |
|---|---|
| ID types | `Guid` for all PKs/FKs |
| Nested collections (Badges, BadHabits) | Separate owned entity tables via EF `OwnsMany().ToTable()` |
| Nested objects (Economy, ActiveStates) | Separate owned entity tables via EF `OwnsOne().ToTable()` |
| UserProfile cardinality | Single-user (one row per device) |
| Startup check | `GetOrCreateUserProfileQuery` runs after credential sync, before onboarding check |
| OnboardingProgressCache FK | Nullable `UserId` column added via new migration; zero data loss |

---

## Pre-Flight Analysis (LEAD_ARCHITECT Role)

**Bounded Context impact:**
- `UserManagement` context introduced: `UserProfile`, `UserEconomy`, `UserActiveStates`, `UserBadge`.
- `GoalManagement` context introduced: `Goal`, `GoalStatus`, `LinkedBadHabit`.
- `Onboarding` context gains cross-aggregate ID reference (`UserId: Guid?` on `OnboardingSession`).

**Zero-Dependency Rule:** Domain stays pure C# — no EF Core, no MAUI, no NuGet packages added to `LifeGrid.Domain`.

**Personas loaded:**
- `SENIOR_NET_DEVELOPER.md` — EF Fluent API, owned entities, migration generation
- `TDD_SPECIALIST.md` — in-memory SQLite integration tests + domain unit tests

**No new NuGet packages required** — `Microsoft.EntityFrameworkCore.Sqlite` (Infrastructure) and `Microsoft.EntityFrameworkCore.InMemory` (Infrastructure.Tests) are already referenced.

---

## Phase A — Domain Entities (Pure C#)

### New files

| File | Type |
|---|---|
| `src/LifeGrid.Domain/UserProfile/UserProfile.cs` | Aggregate root |
| `src/LifeGrid.Domain/UserProfile/UserEconomy.cs` | Owned value object |
| `src/LifeGrid.Domain/UserProfile/UserActiveStates.cs` | Owned value object |
| `src/LifeGrid.Domain/UserProfile/UserBadge.cs` | Owned collection entity |
| `src/LifeGrid.Domain/Goal/Goal.cs` | Aggregate root |
| `src/LifeGrid.Domain/Goal/GoalStatus.cs` | Enum |
| `src/LifeGrid.Domain/Goal/LinkedBadHabit.cs` | Owned collection entity |

### Modified file

`src/LifeGrid.Domain/Onboarding/OnboardingSession.cs` — add `UserId: Guid?` property + `LinkToUser(Guid)` method.

---

### A.1 — `UserProfile.cs`

```csharp
namespace LifeGrid.Domain.UserProfile;

public sealed class UserProfile
{
    private readonly List<UserBadge> _badges = new();
    private UserProfile() { }

    public static UserProfile Create() => new()
    {
        UserId       = Guid.NewGuid(),
        CurrentLevel = 1,
        Economy      = UserEconomy.CreateDefault(),
        ActiveStates = UserActiveStates.CreateDefault()
    };

    public Guid                       UserId       { get; private set; }
    public int                        CurrentLevel { get; private set; }
    public UserEconomy                Economy      { get; private set; } = null!;
    public UserActiveStates           ActiveStates { get; private set; } = null!;
    public IReadOnlyCollection<UserBadge> Badges   => _badges.AsReadOnly();
}
```

---

### A.2 — `UserEconomy.cs`

```csharp
namespace LifeGrid.Domain.UserProfile;

public sealed class UserEconomy
{
    private UserEconomy() { }

    public static UserEconomy CreateDefault() => new()
    {
        LifetimeGpAverage = 0.0,
        LifetimeXp        = 0,
        CurrentSp         = 0,
        ShieldsAvailable  = 0,
        MaxShieldCap      = 2
    };

    public double LifetimeGpAverage { get; private set; }
    public int    LifetimeXp        { get; private set; }
    public int    CurrentSp         { get; private set; }
    public int    ShieldsAvailable  { get; private set; }
    public int    MaxShieldCap      { get; private set; }
}
```

---

### A.3 — `UserActiveStates.cs`

```csharp
namespace LifeGrid.Domain.UserProfile;

public sealed class UserActiveStates
{
    private UserActiveStates() { }

    public static UserActiveStates CreateDefault() => new()
    {
        DoubleXpMode   = false,
        DoubleXpExpiry = DateTime.MinValue
    };

    public bool     DoubleXpMode   { get; private set; }
    public DateTime DoubleXpExpiry { get; private set; }
}
```

---

### A.4 — `UserBadge.cs`

```csharp
namespace LifeGrid.Domain.UserProfile;

public sealed class UserBadge
{
    private UserBadge() { }

    public Guid     BadgeId     { get; private set; }
    public string   BadgeType   { get; private set; } = string.Empty;
    public string   Description { get; private set; } = string.Empty;
    public DateTime DateEarned  { get; private set; }
}
```

*(No factory method yet — badges are awarded by the gamification engine in future phases.)*

---

### A.5 — `GoalStatus.cs`

```csharp
namespace LifeGrid.Domain.Goal;

public enum GoalStatus
{
    Active,
    Overwhelmed,
    Abandoned,
    Completed
}
```

---

### A.6 — `LinkedBadHabit.cs`

```csharp
namespace LifeGrid.Domain.Goal;

public sealed class LinkedBadHabit
{
    private LinkedBadHabit() { }

    public Guid   BadHabitId   { get; private set; }
    public string Description  { get; private set; } = string.Empty;
    public int    DangerLevel  { get; private set; }
}
```

---

### A.7 — `Goal.cs`

```csharp
namespace LifeGrid.Domain.Goal;

public sealed class Goal
{
    private readonly List<LinkedBadHabit> _linkedBadHabits = new();
    private Goal() { }

    public static Goal Create(Guid userId, string description, string ambientTag,
                              string duration, DateTime deadlineDate) => new()
    {
        GoalId       = Guid.NewGuid(),
        UserId       = userId,
        Description  = description,
        AmbientTag   = ambientTag,
        Duration     = duration,
        DeadlineDate = deadlineDate,
        Status       = GoalStatus.Active
    };

    public Guid       GoalId       { get; private set; }
    public Guid       UserId       { get; private set; }
    public string     Description  { get; private set; } = string.Empty;
    public string     AmbientTag   { get; private set; } = string.Empty;
    public string     Duration     { get; private set; } = string.Empty;
    public DateTime   DeadlineDate { get; private set; }
    public GoalStatus Status       { get; private set; }
    public IReadOnlyCollection<LinkedBadHabit> LinkedBadHabits => _linkedBadHabits.AsReadOnly();
}
```

---

### A.8 — `OnboardingSession.cs` modification

Add to the existing class body:

```csharp
public Guid? UserId { get; private set; }

public void LinkToUser(Guid userId) => UserId = userId;
```

---

## Phase B — Application Layer

### New files

| File | Purpose |
|---|---|
| `src/LifeGrid.Application/UserProfile/IUserProfileRepository.cs` | Repository contract |
| `src/LifeGrid.Application/UserProfile/Queries/GetOrCreateUserProfileQuery.cs` | Startup query + handler |

---

### B.1 — `IUserProfileRepository.cs`

```csharp
using LifeGrid.Domain.UserProfile;

namespace LifeGrid.Application.UserProfile;

public interface IUserProfileRepository
{
    Task<Domain.UserProfile.UserProfile?> GetSingleAsync(CancellationToken ct = default);
    Task AddAsync(Domain.UserProfile.UserProfile profile, CancellationToken ct = default);
}
```

*(The `Domain.UserProfile.UserProfile` fully-qualified form avoids namespace collision with the folder name.)*

---

### B.2 — `GetOrCreateUserProfileQuery.cs`

```csharp
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.UserProfile.Queries;

public record GetOrCreateUserProfileQuery : IRequest<Result<Domain.UserProfile.UserProfile>>;

public sealed class GetOrCreateUserProfileQueryHandler(IUserProfileRepository repository)
    : IRequestHandler<GetOrCreateUserProfileQuery, Result<Domain.UserProfile.UserProfile>>
{
    public async Task<Result<Domain.UserProfile.UserProfile>> Handle(
        GetOrCreateUserProfileQuery request,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetSingleAsync(cancellationToken);
        if (existing is not null)
            return Result<Domain.UserProfile.UserProfile>.Success(existing);

        var profile = Domain.UserProfile.UserProfile.Create();
        await repository.AddAsync(profile, cancellationToken);
        return Result<Domain.UserProfile.UserProfile>.Success(profile);
    }
}
```

---

## Phase C — Infrastructure: EF Config, DbContext, Repository

### Modified files

| File | Change |
|---|---|
| `src/LifeGrid.Infrastructure/Data/LifeGridDbContext.cs` | Add `DbSet<UserProfile>` and `DbSet<Goal>` |
| `src/LifeGrid.Infrastructure/Data/EntityConfigurations/OnboardingSessionConfiguration.cs` | Add nullable `UserId` FK |
| `src/LifeGrid.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` | Register `IUserProfileRepository` |

### New files

| File | Purpose |
|---|---|
| `src/LifeGrid.Infrastructure/Data/EntityConfigurations/UserProfileConfiguration.cs` | Full Fluent API for UserProfile + owned types |
| `src/LifeGrid.Infrastructure/Data/EntityConfigurations/GoalConfiguration.cs` | Full Fluent API for Goal + owned types |
| `src/LifeGrid.Infrastructure/Data/Repositories/UserProfileRepository.cs` | Concrete repository |

---

### C.1 — `LifeGridDbContext.cs` changes

Add two DbSets:

```csharp
public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
public DbSet<Goal>        Goals        => Set<Goal>();
```

---

### C.2 — `UserProfileConfiguration.cs`

```csharp
using LifeGrid.Domain.UserProfile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeGrid.Infrastructure.Data.EntityConfigurations;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");
        builder.HasKey(e => e.UserId);
        builder.Property(e => e.UserId).ValueGeneratedNever();
        builder.Property(e => e.CurrentLevel);

        builder.OwnsOne(e => e.Economy, economy =>
        {
            economy.ToTable("UserEconomy");
            economy.Property(e => e.LifetimeGpAverage);
            economy.Property(e => e.LifetimeXp);
            economy.Property(e => e.CurrentSp);
            economy.Property(e => e.ShieldsAvailable);
            economy.Property(e => e.MaxShieldCap);
        });

        builder.OwnsOne(e => e.ActiveStates, states =>
        {
            states.ToTable("UserActiveStates");
            states.Property(e => e.DoubleXpMode);
            states.Property(e => e.DoubleXpExpiry);
        });

        builder.OwnsMany(e => e.Badges, badge =>
        {
            badge.ToTable("UserBadges");
            badge.WithOwner().HasForeignKey("UserId");
            badge.HasKey(b => b.BadgeId);
            badge.Property(b => b.BadgeId).ValueGeneratedNever();
            badge.Property(b => b.BadgeType).HasMaxLength(100);
            badge.Property(b => b.Description).HasMaxLength(2000);
            badge.Property(b => b.DateEarned);
        });
    }
}
```

---

### C.3 — `GoalConfiguration.cs`

```csharp
using LifeGrid.Domain.Goal;
using LifeGrid.Domain.UserProfile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeGrid.Infrastructure.Data.EntityConfigurations;

public sealed class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.ToTable("Goals");
        builder.HasKey(e => e.GoalId);
        builder.Property(e => e.GoalId).ValueGeneratedNever();
        builder.Property(e => e.UserId);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.AmbientTag).HasMaxLength(100);
        builder.Property(e => e.Duration).HasMaxLength(50);
        builder.Property(e => e.DeadlineDate);
        builder.Property(e => e.Status)
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.HasOne<UserProfile>()
               .WithMany()
               .HasForeignKey(e => e.UserId)
               .IsRequired()
               .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany(e => e.LinkedBadHabits, habit =>
        {
            habit.ToTable("GoalLinkedBadHabits");
            habit.WithOwner().HasForeignKey("GoalId");
            habit.HasKey(h => h.BadHabitId);
            habit.Property(h => h.BadHabitId).ValueGeneratedNever();
            habit.Property(h => h.Description).HasMaxLength(2000);
            habit.Property(h => h.DangerLevel);
        });
    }
}
```

---

### C.4 — `OnboardingSessionConfiguration.cs` change

Add at the end of `Configure()`:

```csharp
builder.Property(e => e.UserId);
builder.HasOne<UserProfile>()
       .WithMany()
       .HasForeignKey(e => e.UserId)
       .IsRequired(false)
       .OnDelete(DeleteBehavior.SetNull);
```

---

### C.5 — `UserProfileRepository.cs`

```csharp
using LifeGrid.Application.UserProfile;
using LifeGrid.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LifeGrid.Infrastructure.Data.Repositories;

public sealed class UserProfileRepository(LifeGridDbContext db) : IUserProfileRepository
{
    public Task<Domain.UserProfile.UserProfile?> GetSingleAsync(CancellationToken ct = default)
        => db.UserProfiles.FirstOrDefaultAsync(ct);

    public async Task AddAsync(Domain.UserProfile.UserProfile profile, CancellationToken ct = default)
    {
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
    }
}
```

*(Owned entities `Economy`, `ActiveStates`, and `Badges` are automatically included by EF Core for owned types.)*

---

### C.6 — `InfrastructureServiceExtensions.cs` change

Add one registration inside `AddInfrastructure()`:

```csharp
services.AddScoped<IUserProfileRepository, UserProfileRepository>();
```

---

## Phase D — Migration Generation

Run on host machine after C is complete:

```powershell
dotnet ef migrations add AddUserProfileAndGoalSchema --project src/LifeGrid.Infrastructure
```

**Verify the generated migration:**
- Up() creates: `UserProfiles`, `UserEconomy`, `UserActiveStates`, `UserBadges`, `Goals`, `GoalLinkedBadHabits`.
- Up() alters: `OnboardingProgressCache` — adds nullable `UserId TEXT` column + FK constraint.
- Down() reverses all of the above cleanly.
- `OnboardingProgressCache` existing columns and data are **not modified**.

---

## Phase E — Presentation: Startup Integration

### Modified file: `src/LifeGrid.Presentation/App.xaml.cs`

Update `OnStart()` — insert `GetOrCreateUserProfileQuery` as step 2:

```csharp
protected override async void OnStart()
{
    base.OnStart();
    await _credentialSync.SyncAsync();                                     // Phase 3
    await _mediator.Send(new GetOrCreateUserProfileQuery());                // Phase 4
    var result = await _mediator.Send(new GetOrCreateOnboardingSessionQuery());
    if (result.IsSuccess && !result.Value!.IsComplete)
        await Shell.Current.GoToAsync("setup");
}
```

No new constructor parameters — `IMediator` is already injected.

---

## Phase F — TDD: Domain Unit Tests

### New file: `tests/LifeGrid.Domain.Tests/UserProfile/UserProfileTests.cs`

Seven tests covering `UserProfile.Create()` defaults:

| Test | Assert |
|---|---|
| `Create_GeneratesNonEmptyGuid` | `UserId != Guid.Empty` |
| `Create_TwoProfiles_HaveDifferentIds` | `a.UserId != b.UserId` |
| `Create_SetsLevelToOne` | `CurrentLevel == 1` |
| `Create_Economy_AllDefaultsAreZero` | `LifetimeXp == 0 && CurrentSp == 0 && ShieldsAvailable == 0` |
| `Create_Economy_MaxShieldCapIsTwo` | `MaxShieldCap == 2` |
| `Create_ActiveStates_DoubleXpModeIsFalse` | `DoubleXpMode == false` |
| `Create_HasEmptyBadgesCollection` | `Badges.Count == 0` |

### New file: `tests/LifeGrid.Domain.Tests/Goal/GoalTests.cs`

Three tests:

| Test | Assert |
|---|---|
| `Create_GeneratesNonEmptyGuid` | `GoalId != Guid.Empty` |
| `Create_SetsStatusToActive` | `Status == GoalStatus.Active` |
| `Create_HasEmptyLinkedBadHabitsCollection` | `LinkedBadHabits.Count == 0` |

### Modified file: `tests/LifeGrid.Domain.Tests/Onboarding/OnboardingSessionTests.cs`

Add one test:

| Test | Assert |
|---|---|
| `LinkToUser_SetsUserId` | After `LinkToUser(guid)`, `UserId == guid` |

---

## Phase G — TDD: Infrastructure Integration Tests

### New file: `tests/LifeGrid.Infrastructure.Tests/Schema/UserProfileSchemaTests.cs`

Uses real SQLite `:memory:` with `context.Database.Migrate()`.

Test fixture pattern:
```csharp
private static LifeGridDbContext CreateMigratedContext()
{
    var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
    connection.Open();
    var options = new DbContextOptionsBuilder<LifeGridDbContext>()
        .UseSqlite(connection)
        .Options;
    var ctx = new LifeGridDbContext(options);
    ctx.Database.Migrate();
    return ctx;
}
```

Four tests:

| Test | Assert |
|---|---|
| `UserProfile_CanBeWrittenAndReadBack` | Written profile round-trips; `CurrentLevel == 1` |
| `UserProfile_Economy_PersistedInSeparateTable` | `Economy.MaxShieldCap == 2` after round-trip |
| `UserProfile_ActiveStates_PersistedInSeparateTable` | `ActiveStates.DoubleXpMode == false` after round-trip |
| `UserProfile_Badges_CanBeWrittenAndReadBack` | Badge with known `BadgeId` survives round-trip |

*(EF `OwnsOne().ToTable()` puts Economy/ActiveStates in separate SQL tables; round-trip proves the FK/shared-PK wiring is correct.)*

### New file: `tests/LifeGrid.Infrastructure.Tests/Schema/GoalSchemaTests.cs`

Three tests:

| Test | Assert |
|---|---|
| `Goal_CanBeWrittenAndReadBack` | `Status == "Active"` (stored as string) |
| `Goal_LinkedBadHabit_CanBeWrittenAndReadBack` | Bad habit with known `BadHabitId` survives round-trip |
| `OnboardingProgressCache_UserIdColumn_IsNullable` | Existing session row has `null` `UserId` after migration |

---

## Phase H — Build & Test Verification

1. `dotnet build LifeGrid.slnx` → 0 errors, 0 warnings.
2. `dotnet test` → all 28 existing tests still pass; ≥14 new tests pass.
3. Verify migration file was generated (not hand-authored) and matches expected table set.
4. Confirm `Down()` reversal in migration compiles cleanly.

---

## File Inventory

### New files

| File | Layer |
|---|---|
| `src/LifeGrid.Domain/UserProfile/UserProfile.cs` | Domain |
| `src/LifeGrid.Domain/UserProfile/UserEconomy.cs` | Domain |
| `src/LifeGrid.Domain/UserProfile/UserActiveStates.cs` | Domain |
| `src/LifeGrid.Domain/UserProfile/UserBadge.cs` | Domain |
| `src/LifeGrid.Domain/Goal/Goal.cs` | Domain |
| `src/LifeGrid.Domain/Goal/GoalStatus.cs` | Domain |
| `src/LifeGrid.Domain/Goal/LinkedBadHabit.cs` | Domain |
| `src/LifeGrid.Application/UserProfile/IUserProfileRepository.cs` | Application |
| `src/LifeGrid.Application/UserProfile/Queries/GetOrCreateUserProfileQuery.cs` | Application |
| `src/LifeGrid.Infrastructure/Data/EntityConfigurations/UserProfileConfiguration.cs` | Infrastructure |
| `src/LifeGrid.Infrastructure/Data/EntityConfigurations/GoalConfiguration.cs` | Infrastructure |
| `src/LifeGrid.Infrastructure/Data/Repositories/UserProfileRepository.cs` | Infrastructure |
| `src/LifeGrid.Infrastructure/Migrations/<timestamp>_AddUserProfileAndGoalSchema.cs` *(generated)* | Infrastructure |
| `tests/LifeGrid.Domain.Tests/UserProfile/UserProfileTests.cs` | Tests |
| `tests/LifeGrid.Domain.Tests/Goal/GoalTests.cs` | Tests |
| `tests/LifeGrid.Infrastructure.Tests/Schema/UserProfileSchemaTests.cs` | Tests |
| `tests/LifeGrid.Infrastructure.Tests/Schema/GoalSchemaTests.cs` | Tests |

### Modified files

| File | Change |
|---|---|
| `src/LifeGrid.Domain/Onboarding/OnboardingSession.cs` | Add `UserId: Guid?` + `LinkToUser()` method |
| `src/LifeGrid.Infrastructure/Data/LifeGridDbContext.cs` | Add `DbSet<UserProfile>`, `DbSet<Goal>` |
| `src/LifeGrid.Infrastructure/Data/EntityConfigurations/OnboardingSessionConfiguration.cs` | Add nullable FK `UserId` → `UserProfiles` |
| `src/LifeGrid.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` | Register `IUserProfileRepository` |
| `src/LifeGrid.Presentation/App.xaml.cs` | Add `GetOrCreateUserProfileQuery` call in `OnStart()` |
| `tests/LifeGrid.Domain.Tests/Onboarding/OnboardingSessionTests.cs` | Add `LinkToUser_SetsUserId` test |
