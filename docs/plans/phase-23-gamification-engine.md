# Phase 23 — Gamification Engine: Economy Math & Global State Sync
**Status: DONE**

---

## Decisions Made

| Question | Answer |
|---|---|
| XP/SP tier basis | **Per-entry** — tier determined by `ActualValue ÷ TargetValue` for the single submitted log entry |
| Double XP Mode | **Deferred entirely** — Phase 23 ignores `DoubleXpMode` flag and Flash-specific activation |
| Global reactivity pattern | **WeakReferenceMessenger** via `IEconomyStateBroadcaster` abstraction in Application |

---

## Phase A — Domain Layer: GamificationCalculationEngine

### A.1 — New enum: `ProofTier`

**File:** `src/LifeGrid.Domain/Gamification/ProofTier.cs`
```csharp
namespace LifeGrid.Domain.Gamification;

public enum ProofTier { Proven, PartiallyProven, Unproven }
```

### A.2 — New record: `EntryReward`

**File:** `src/LifeGrid.Domain/Gamification/EntryReward.cs`
```csharp
namespace LifeGrid.Domain.Gamification;

public record EntryReward(int XpEarned, int SpEarned);
```

### A.3 — New static class: `GamificationCalculationEngine`

**File:** `src/LifeGrid.Domain/Gamification/GamificationCalculationEngine.cs`

```csharp
using LifeGrid.Domain.Habit;
namespace LifeGrid.Domain.Gamification;

public static class GamificationCalculationEngine
{
    public const int LevelThresholdXp = 300;

    // Per-entry reward based on this single submission's ratio
    public static EntryReward CalculateEntryReward(
        HabitType habitType, double actualValue, double targetValue, bool hasProof)
    {
        // Double XP (Flash / DoubleXpMode) deferred to a later phase
        var tier = DetermineProofTier(actualValue, targetValue, hasProof);
        return tier switch
        {
            ProofTier.Proven          => new EntryReward(20, 4),
            ProofTier.PartiallyProven => new EntryReward(10, 2),
            _                         => new EntryReward(3,  1)   // Unproven
        };
    }

    // GP for a single habit: cumulative completion capped at 100 (stored as 0–100 float)
    public static double CalculateHabitGp(double cumulativeTotalActual, double targetValue)
        => targetValue <= 0 ? 0.0 : Math.Min(cumulativeTotalActual / targetValue * 100.0, 100.0);

    // WeekGoal GP: average of all non-MomentBurst habits' completion percentages (0–100 float)
    public static double CalculateWeekGoalGp(
        IReadOnlyList<(double CumulativeTotal, double TargetValue, HabitType HabitType)> habitSummaries)
    {
        var eligibleHabits = habitSummaries
            .Where(h => h.HabitType != HabitType.MomentBurst)
            .ToList();

        if (eligibleHabits.Count == 0) return 0.0;

        return eligibleHabits.Average(h => CalculateHabitGp(h.CumulativeTotal, h.TargetValue));
    }

    // Returns the new level given total lifetime XP (1-based, minimum 1)
    public static int CalculateLevel(int lifetimeXp, int levelThreshold = LevelThresholdXp)
        => Math.Max(1, lifetimeXp / levelThreshold + 1);

    private static ProofTier DetermineProofTier(double actualValue, double targetValue, bool hasProof)
    {
        if (!hasProof) return ProofTier.Unproven;
        var ratio = targetValue > 0 ? actualValue / targetValue : 0.0;
        return ratio >= 0.75 ? ProofTier.Proven : ProofTier.PartiallyProven;
    }
}
```

**Rules:**
- `MomentBurst` habits are excluded from GP average (Section 4.3.1).
- `Flash` habits are included in GP (only MomentBurst is explicitly excluded).
- Level = `lifetimeXp / 300 + 1` (integer division). Rollover example: 280 + 50 = 330 total → Level 2, 30 XP carried toward Level 3.
- GP stored as 0–100 float (not 0–1).

---

### A.4 — Modify `UserEconomy.cs`

**File:** `src/LifeGrid.Domain/UserProfile/UserEconomy.cs`

Add two new `internal` methods:

```csharp
// Adds SP and converts every complete 30 SP milestone into a Shield
internal void GrantSp(int amount)
{
    CurrentSp += amount;
    while (CurrentSp >= 30)
    {
        CurrentSp -= 30;
        GrantShield();
    }
}

internal void SetLifetimeGpAverage(double value) => LifetimeGpAverage = value;
```

---

### A.5 — Modify `UserProfile.cs`

**File:** `src/LifeGrid.Domain/UserProfile/UserProfile.cs`

Add two new `public` methods:

```csharp
public void GrantSp(int amount) => Economy.GrantSp(amount);

public void ApplyXpAndLevelProgression(int xpEarned)
{
    Economy.GrantXp(xpEarned);
    CurrentLevel = GamificationCalculationEngine.CalculateLevel(Economy.LifetimeXp);
}

public void UpdateLifetimeGpAverage(double average) => Economy.SetLifetimeGpAverage(average);
```

Note: `GamificationCalculationEngine` is in `LifeGrid.Domain.Gamification` — add `using` directive. No external dependency added.

---

### A.6 — Modify `WeekGoal.cs`

**File:** `src/LifeGrid.Domain/WeekGoal/WeekGoal.cs`

Add one new `public` method (replaces the two `internal` set methods for the Application layer):

```csharp
public void RecordMetricsUpdate(double newGp, int additionalXpEarned)
{
    GoalWeeklyGp       = newGp;
    GoalWeeklyXpEarned += additionalXpEarned;
}
```

---

### A.7 — Modify `Week.cs`

**File:** `src/LifeGrid.Domain/Week/Week.cs`

Add one new `public` method:

```csharp
public void AddSpEarned(int amount) => TotalWeeklySpEarned += amount;
```

---

## Phase B — Application Layer: Gamification Pipeline Integration

### B.1 — New record: `EconomyStateMutatedMessage`

**File:** `src/LifeGrid.Application/Gamification/EconomyStateMutatedMessage.cs`
```csharp
namespace LifeGrid.Application.Gamification;

public record EconomyStateMutatedMessage;
```
Marker type — subscribers call `LoadAsync()` on receipt to pull fresh data.

### B.2 — New interface: `IEconomyStateBroadcaster`

**File:** `src/LifeGrid.Application/Gamification/IEconomyStateBroadcaster.cs`
```csharp
namespace LifeGrid.Application.Gamification;

public interface IEconomyStateBroadcaster
{
    void Broadcast();
}
```

### B.3 — New record: `HabitCompletionSummaryDto`

**File:** `src/LifeGrid.Application/Gamification/HabitCompletionSummaryDto.cs`
```csharp
using LifeGrid.Domain.Habit;
namespace LifeGrid.Application.Gamification;

public record HabitCompletionSummaryDto(
    Guid      HabitId,
    double    TargetValue,
    double    TotalActualValue,
    HabitType HabitType);
```

### B.4 — Extend `IHabitRepository`

**File:** `src/LifeGrid.Application/Habit/IHabitRepository.cs`

Add:
```csharp
Task<IReadOnlyList<HabitCompletionSummaryDto>> GetCompletionSummariesForWeekGoalAsync(
    Guid weekGoalId, CancellationToken ct = default);
```
(Add `using LifeGrid.Application.Gamification;` to the file.)

### B.5 — Extend `IWeekRepository`

**File:** `src/LifeGrid.Application/Week/IWeekRepository.cs`

Add two methods:
```csharp
Task<WeekGoalEntity?> GetWeekGoalByIdAsync(Guid weekGoalId, CancellationToken ct = default);
Task<(double GpSum, int GpCount)> GetWeekGoalGpStatsAsync(CancellationToken ct = default);
```

### B.6 — Refactor `LogHabitProgressCommandHandler`

**File:** `src/LifeGrid.Application/HabitLogging/LogHabitProgressCommandHandler.cs`

New constructor signature:
```csharp
public sealed class LogHabitProgressCommandHandler(
    IHabitRepository         habitRepository,
    IWeekRepository          weekRepository,
    IUserProfileRepository   userProfileRepository,
    IDateTimeProvider        dateTimeProvider,
    IUnitOfWork              unitOfWork,
    IEconomyStateBroadcaster broadcaster)
```

Full `Handle` logic:
```
1.  Validate ActualValue > 0.
2.  Load habit by HabitId; return Failure if not found.
3.  Load WeekGoal by habit.WeekGoalId via weekRepository.GetWeekGoalByIdAsync; return Failure if null.
4.  Load Week by weekGoal.WeekId via weekRepository.GetByIdAsync; return Failure if null.
5.  Load UserProfile via userProfileRepository.GetSingleAsync; return Failure if null.
6.  Create CompletedValueLog and call habitRepository.AddCompletionLogAsync (same as Phase 22).
7.  Load HabitCompletionSummaryDtos for the WeekGoal (DB query, returns old totals before commit).
8.  Adjust summaries: for the habit being logged, add request.ActualValue to its TotalActualValue.
9.  bool hasProof = request.ProofText is not null || request.ProofImageUrl is not null.
10. EntryReward reward = GamificationCalculationEngine.CalculateEntryReward(
        habit.HabitType, request.ActualValue, habit.TargetValue, hasProof).
11. double newWeekGoalGp = GamificationCalculationEngine.CalculateWeekGoalGp(
        adjustedSummaries.Select(s => (s.TotalActualValue, s.TargetValue, s.HabitType)).ToList()).
12. Capture oldGp = weekGoal.GoalWeeklyGp.
13. Get (gpSum, gpCount) = await weekRepository.GetWeekGoalGpStatsAsync(ct).
14. double newLifetimeGpAvg = gpCount > 0 ? (gpSum - oldGp + newWeekGoalGp) / gpCount : newWeekGoalGp.
15. Apply mutations:
    a. weekGoal.RecordMetricsUpdate(newWeekGoalGp, reward.XpEarned)
    b. week.AddSpEarned(reward.SpEarned)
    c. profile.GrantSp(reward.SpEarned)
    d. profile.ApplyXpAndLevelProgression(reward.XpEarned)
    e. profile.UpdateLifetimeGpAverage(newLifetimeGpAvg)
16. await unitOfWork.CommitAsync(ct)  — single atomic transaction for all mutations.
17. broadcaster.Broadcast()
18. return Result.Success()
```

---

## Phase C — Infrastructure Layer: Repository Queries

### C.1 — `HabitRepository` — implement `GetCompletionSummariesForWeekGoalAsync`

**File:** `src/LifeGrid.Infrastructure/Data/Repositories/HabitRepository.cs`

```csharp
public async Task<IReadOnlyList<HabitCompletionSummaryDto>> GetCompletionSummariesForWeekGoalAsync(
    Guid weekGoalId, CancellationToken ct = default)
    => await db.Habits
        .Where(h => h.WeekGoalId == weekGoalId)
        .Select(h => new HabitCompletionSummaryDto(
            h.HabitId,
            h.TargetValue,
            db.CompletedValueLogs
                .Where(l => l.HabitId == h.HabitId)
                .Sum(l => (double?)l.ActualValue) ?? 0.0,
            h.HabitType))
        .ToListAsync(ct);
```
Uses a correlated subquery projection; hits the DB directly (bypasses EF change-tracker to avoid double-counting the staged new log).

### C.2 — `WeekRepository` — implement `GetWeekGoalByIdAsync`

```csharp
public Task<WeekGoalEntity?> GetWeekGoalByIdAsync(Guid weekGoalId, CancellationToken ct = default)
    => db.WeekGoals.FirstOrDefaultAsync(wg => wg.WeekGoalId == weekGoalId, ct);
```
Returns a tracked `WeekGoal` — EF detects mutations via change-tracking snapshot; no explicit `Update()` call needed.

### C.3 — `WeekRepository` — implement `GetWeekGoalGpStatsAsync`

```csharp
public async Task<(double GpSum, int GpCount)> GetWeekGoalGpStatsAsync(CancellationToken ct = default)
{
    var count = await db.WeekGoals.CountAsync(ct);
    var sum   = count > 0 ? await db.WeekGoals.SumAsync(wg => wg.GoalWeeklyGp, ct) : 0.0;
    return (sum, count);
}
```

**No EF migration required** — all queries operate on existing tables and columns.

---

## Phase D — Presentation Layer: Global State Reactivity

### D.1 — New class: `WeakReferenceMessengerBroadcaster`

**File:** `src/LifeGrid.Presentation/Services/WeakReferenceMessengerBroadcaster.cs`

```csharp
using CommunityToolkit.Mvvm.Messaging;
using LifeGrid.Application.Gamification;

namespace LifeGrid.Presentation.Services;

internal sealed class WeakReferenceMessengerBroadcaster : IEconomyStateBroadcaster
{
    public void Broadcast()
        => WeakReferenceMessenger.Default.Send(new EconomyStateMutatedMessage());
}
```

### D.2 — `HudViewModel` — subscribe

In constructor, add:
```csharp
WeakReferenceMessenger.Default.Register<EconomyStateMutatedMessage>(this,
    async (_, _) => await LoadAsync());
```

### D.3 — `HomeViewModel` — subscribe

In constructor, add:
```csharp
WeakReferenceMessenger.Default.Register<EconomyStateMutatedMessage>(this,
    async (_, _) => await LoadAsync());
```
(Triggers reload of current-week habits with refreshed GP values.)

### D.4 — `WeeklyHabitsViewModel` — subscribe

In constructor, add:
```csharp
WeakReferenceMessenger.Default.Register<EconomyStateMutatedMessage>(this,
    async (_, _) => await LoadAsync());
```

### D.5 — `TimelineViewModel` — subscribe

In constructor, add:
```csharp
WeakReferenceMessenger.Default.Register<EconomyStateMutatedMessage>(this,
    async (_, _) => await LoadAsync());
```

### D.6 — `MauiProgram.cs` — DI registration

Add:
```csharp
builder.Services.AddSingleton<IEconomyStateBroadcaster, WeakReferenceMessengerBroadcaster>();
```
Singleton so the same instance is injected everywhere (important for WeakReferenceMessenger to work with a shared Default instance).

---

## Phase E — TDD

### E.1 — New: `GamificationCalculationEngineTests.cs`

**File:** `tests/LifeGrid.Domain.Tests/Gamification/GamificationCalculationEngineTests.cs`

| Test | Assert |
|---|---|
| `CalculateEntryReward_ProvenAbove75pct_Returns20Xp4Sp` | Actual=4, Target=5 (80%), hasProof=true → XP=20, SP=4 |
| `CalculateEntryReward_PartiallyProvenBelow75pct_Returns10Xp2Sp` | Actual=3, Target=5 (60%), hasProof=true → XP=10, SP=2 |
| `CalculateEntryReward_Unproven_Returns3Xp1Sp` | hasProof=false (any ratio) → XP=3, SP=1 |
| `CalculateEntryReward_ProvenExactly75pct_IsProven` | Actual=3, Target=4 (75%), hasProof=true → XP=20, SP=4 |
| `CalculateWeekGoalGp_ExcludesMomentBurstHabits` | 1 Planned at 80%, 1 MomentBurst at 100% → GP=80.0 |
| `CalculateWeekGoalGp_CapsIndividualHabitAt100` | Actual=200, Target=100 → habit GP capped at 100.0 |
| `CalculateLevel_LevelRollover_290XpPlus25_SetsLevel2` | LifetimeXp = 315 → Level 2; remainder = 15 XP toward next level (315 % 300) |
| `CalculateLevel_ExactThreshold_AdvancesLevel` | LifetimeXp = 300 → Level 2 |
| `UserEconomy_GrantSp_Milestone30_GrantsShield` | Start: CurrentSp=28, ShieldsAvailable=0; GrantSp(5) → CurrentSp=3, ShieldsAvailable=1 |
| `UserEconomy_GrantSp_DoesNotExceedShieldCap` | MaxShieldCap=2, ShieldsAvailable=2, CurrentSp=0; GrantSp(35) → ShieldsAvailable=2 (capped) |

*Note: `UserEconomy.GrantSp` tests live in the existing `UserProfileTests.cs` file or a new `UserEconomyTests.cs` in `LifeGrid.Domain.Tests/UserProfile/`.*

### E.2 — Modify: `LogHabitProgressCommandTests.cs`

**File:** `tests/LifeGrid.Application.Tests/HabitLogging/LogHabitProgressCommandTests.cs`

**Constructor changes:** add mocks for `IWeekRepository`, `IUserProfileRepository`, `IEconomyStateBroadcaster`. Wire defaults: WeekGoal mock returns a seeded WeekGoal, UserProfile mock returns a seeded UserProfile, GetCompletionSummariesForWeekGoalAsync returns empty list by default, GetWeekGoalGpStatsAsync returns (0.0, 1).

**Existing 5 tests:** update constructor setup to satisfy new dependencies (no assertions change).

**New tests (3):**

| Test | Assert |
|---|---|
| `HappyPath_CallsBroadcasterExactlyOnce` | After successful handle, `IEconomyStateBroadcaster.Broadcast()` received exactly 1 time |
| `HappyPath_AppliesXpToUserProfile` | Captured UserProfile has `Economy.LifetimeXp > 0` after handle |
| `HappyPath_RecordsGpOnWeekGoal` | Captured WeekGoal argument passed to `RecordMetricsUpdate` has GP > 0 when habit has existing completion |

*Note: testing GP and UserProfile mutations via NSubstitute `Arg.Do` capture on repository calls; alternatively use real in-memory profile/weekgoal objects in the test.*

---

## Acceptance Criteria

- `dotnet build LifeGrid.slnx` → 0 errors, 0 warnings.
- `dotnet test` → **286 tests pass** (95 domain + 125 application + 66 infrastructure).
- Logging a habit via `HabitLoggingPage`:
  - XP and SP are added to `UserProfile.Economy` (verified via HUD before/after).
  - HUD updates instantly on the same screen without navigating away (WeakReferenceMessenger fires).
  - GP for the parent WeekGoal updates in the Home and Timeline views without a manual pull-to-refresh.
- Level progression: if logging causes total XP to cross a 300-XP boundary, `CurrentLevel` increments in the same commit.
- SP milestone: if logging causes `CurrentSp` to reach or exceed 30, a shield is granted and SP resets.
- Providing proof (text or image) on a log entry that covers ≥75% of the target → 20 XP, 4 SP awarded.
- No EF migration required — schema is unchanged.

---

## Implementation Notes (Post-Approval Corrections)

### 1. WeakReferenceMessenger — generic overload required
The plan showed the single-generic `Register<TMessage>(this, handler)` overload. This resolves to a handler where the recipient `r` is typed as `object`, causing CS1061. The two-type-parameter overload must be used:
```csharp
WeakReferenceMessenger.Default.Register<HudViewModel, EconomyStateMutatedMessage>(this,
    async (r, _) => await r.LoadAsync());
```
Applied to all four subscriber ViewModels.

### 2. `HudViewModel` — Singleton + Scoped DbContext conflict
`HudViewModel` is a DI Singleton. Injecting `IMediator` directly means every `LoadAsync()` call resolves repositories from the root DI scope, giving a long-lived `DbContext` that EF's identity map never refreshes. Fixed by injecting `IServiceScopeFactory` and creating a fresh scope per `LoadAsync()` call.

### 3. `HudViewModel.LoadAsync()` — main-thread dispatch
`Broadcast()` fires from a thread pool thread (post-`await CommitAsync()`). All property assignments in `LoadAsync()` are wrapped in `MainThread.BeginInvokeOnMainThread` to ensure bindings update correctly.

### 4. `AppShell` — initial HUD load
`HudViewModel.LoadAsync()` was never called on startup. Added `Loaded += async (_, _) => await hudViewModel.LoadAsync()` in `AppShell.xaml.cs`.

### 5. `GetHudTelemetryQuery` — wrong week selected
All weeks in the DB have `Status = Active` (status is set at creation and never changed). `GetActiveAsync()` returned an arbitrary row — typically the oldest week, not the current one. Fixed by injecting `IDateTimeProvider`, computing the current Monday, and calling `GetByStartDateAsync(currentMonday)`. `GetHudTelemetryQueryTests` updated to use `IDateTimeProvider` mock and `GetByStartDateAsync` stubs, plus one new test asserting Monday-stripping for mid-week dates.
