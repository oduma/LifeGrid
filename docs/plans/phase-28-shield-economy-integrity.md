# Phase 28 — Shield Economy Integrity & SP Deficit Mechanics
**Status: DONE**

---

## Decisions Made

| Question | Answer |
|---|---|
| ShieldEconomyEngine placement | Static class in `LifeGrid.Domain/Gamification/` — same pattern as `GamificationCalculationEngine` |
| Existing SP bug | `UserEconomy.GrantSp` silently loses SP when at max shield cap. Fixed by adding cap-at-30 logic in `ApplySpGain` |
| Negative SP (Deep Deficit) | Explicitly allowed per §3.5 — `ApplySpDeduction` returns negative values. Recovery requires logging to reach 0 |
| Cap-at-max-shields behavior | SP stops converting but stays at exactly 30 (no loss, no accumulation above threshold) |
| Broadcaster enrichment | `IEconomyStateBroadcaster` gains `BroadcastEconomy(int, int)`; `Broadcast()` kept for non-SP broadcasts (Hibernate) |
| Deep Deficit HUD color | `SpCurrentColor` Color property in `HudViewModel`, bound to the `SpCurrent` Span's `TextColor` in `HudView.xaml` |
| EF migration | None needed — SQLite `INTEGER` supports negative values, no schema changes |

---

## Phase A — Domain Layer: ShieldEconomyEngine

### A.1 — New static class: `ShieldEconomyEngine`

**File:** `src/LifeGrid.Domain/Gamification/ShieldEconomyEngine.cs`

```csharp
namespace LifeGrid.Domain.Gamification;

public static class ShieldEconomyEngine
{
    public const int SpConversionThreshold = 30;

    public static (int NewCurrentSp, int NewShieldsAvailable) ApplySpGain(
        int currentSp, int shieldsAvailable, int maxShieldCap, int amount)
    {
        var sp = currentSp + amount;
        while (sp >= SpConversionThreshold && shieldsAvailable < maxShieldCap)
        {
            sp -= SpConversionThreshold;
            shieldsAvailable++;
        }
        // At max cap: hold SP at the threshold — no conversion, no accumulation above 30.
        if (shieldsAvailable >= maxShieldCap && sp > SpConversionThreshold)
            sp = SpConversionThreshold;
        return (sp, shieldsAvailable);
    }

    // Allows result to be negative (intentional Deep Deficit per §3.5).
    public static int ApplySpDeduction(int currentSp, int amount)
        => currentSp - amount;
}
```

### A.2 — Modify `UserEconomy`

**File:** `src/LifeGrid.Domain/UserProfile/UserEconomy.cs`

Replace `GrantSp` body and add `DeductSp`:

```csharp
internal void GrantSp(int amount)
{
    var (newSp, newShields) = ShieldEconomyEngine.ApplySpGain(
        CurrentSp, ShieldsAvailable, MaxShieldCap, amount);
    CurrentSp        = newSp;
    ShieldsAvailable = newShields;
}

internal void DeductSp(int amount)
    => CurrentSp = ShieldEconomyEngine.ApplySpDeduction(CurrentSp, amount);
```

Add `using LifeGrid.Domain.Gamification;` at top of file.

The `GrantShield()` internal method is retained (still used by `GrantSurveyBonusShield`). It is no longer called from `GrantSp`.

### A.3 — Modify `UserProfile`

**File:** `src/LifeGrid.Domain/UserProfile/UserProfile.cs`

Add passthrough:

```csharp
public void DeductSp(int amount) => Economy.DeductSp(amount);
```

---

## Phase B — Application Layer: Broadcaster Enrichment

### B.1 — Enrich `EconomyStateMutatedMessage`

**File:** `src/LifeGrid.Application/Gamification/EconomyStateMutatedMessage.cs`

```csharp
namespace LifeGrid.Application.Gamification;

// CurrentSp/ShieldsAvailable carry post-mutation values for SP-changing broadcasts.
// Non-SP broadcasts (Hibernate) use default (0, 0); subscribers call LoadAsync() regardless.
public record EconomyStateMutatedMessage(int CurrentSp = 0, int ShieldsAvailable = 0);
```

### B.2 — Update `IEconomyStateBroadcaster`

**File:** `src/LifeGrid.Application/Gamification/IEconomyStateBroadcaster.cs`

```csharp
namespace LifeGrid.Application.Gamification;

public interface IEconomyStateBroadcaster
{
    // For structural broadcasts that do not mutate SP or shields (e.g., Hibernate).
    void Broadcast();
    // For SP-mutating operations — carries post-commit economy snapshot.
    void BroadcastEconomy(int currentSp, int shieldsAvailable);
}
```

### B.3 — Update `LogHabitProgressCommandHandler`

**File:** `src/LifeGrid.Application/HabitLogging/LogHabitProgressCommandHandler.cs`

After `await unitOfWork.CommitAsync(ct)`, replace:
```csharp
broadcaster.Broadcast();
```
with:
```csharp
broadcaster.BroadcastEconomy(profile.Economy.CurrentSp, profile.Economy.ShieldsAvailable);
```

`profile` is already in scope (loaded earlier in the handler). After `GrantSp` and `CommitAsync`, EF's change-tracking snapshot reflects the updated values.

### B.4 — Update `PauseWeekCommandHandler`

**File:** `src/LifeGrid.Application/Week/PauseWeekCommand.cs`

- **`HibernateAsync`:** no SP change → keep `broadcaster.Broadcast()` unchanged.
- **`FreezeAsync`:** profile is already loaded and `ConsumeShield()` has run. Replace:
  ```csharp
  broadcaster.Broadcast();
  ```
  with:
  ```csharp
  broadcaster.BroadcastEconomy(profile.Economy.CurrentSp, profile.Economy.ShieldsAvailable);
  ```

---

## Phase C — Presentation Layer

### C.1 — Update `WeakReferenceMessengerBroadcaster`

**File:** `src/LifeGrid.Presentation/Services/WeakReferenceMessengerBroadcaster.cs`

```csharp
using CommunityToolkit.Mvvm.Messaging;
using LifeGrid.Application.Gamification;

namespace LifeGrid.Presentation.Services;

internal sealed class WeakReferenceMessengerBroadcaster : IEconomyStateBroadcaster
{
    public void Broadcast()
        => WeakReferenceMessenger.Default.Send(new EconomyStateMutatedMessage());

    public void BroadcastEconomy(int currentSp, int shieldsAvailable)
        => WeakReferenceMessenger.Default.Send(
               new EconomyStateMutatedMessage(currentSp, shieldsAvailable));
}
```

### C.2 — Update `HudViewModel`

**File:** `src/LifeGrid.Presentation/ViewModels/HudViewModel.cs`

Add Deep Deficit state:

```csharp
// Error color (#FF1B77) when SP is negative (Deep Deficit §3.5);
// normal OnSurface color otherwise.
private static readonly Color NormalSpColor  = Color.FromArgb("#CACACA");
private static readonly Color DeficitSpColor = Color.FromArgb("#FF1B77");

[NotifyPropertyChangedFor(nameof(SpCurrentColor))]
[ObservableProperty] private bool _isInDeepDeficit;

public Color SpCurrentColor => IsInDeepDeficit ? DeficitSpColor : NormalSpColor;
```

In `LoadAsync`, after receiving the DTO:
```csharp
IsInDeepDeficit = d.CurrentSp < 0;
SpCurrent       = d.CurrentSp.ToString();
```

Add `using Microsoft.Maui.Graphics;` at top.

### C.3 — Update `HudView.xaml`

**File:** `src/LifeGrid.Presentation/Controls/HudView.xaml`

Replace the SP current `Span`'s static `TextColor` with a binding:

```xaml
<!-- Before -->
<Span Text="{Binding SpCurrent}" TextColor="{StaticResource OnSurface}" FontFamily="ShareTechMono" />

<!-- After -->
<Span Text="{Binding SpCurrent}" TextColor="{Binding SpCurrentColor}" FontFamily="ShareTechMono" />
```

Only the `SpCurrent` span changes; all other spans in the SP row remain unchanged.

---

## Phase D — TDD

### D.1 — New: `ShieldEconomyEngineTests.cs`

**File:** `tests/LifeGrid.Domain.Tests/Gamification/ShieldEconomyEngineTests.cs`

| # | Test name | Covers |
|---|---|---|
| 1 | `ApplySpGain_BelowThreshold_NoShieldGranted` | SP=15 + 10 → SP=25, shields unchanged |
| 2 | `ApplySpGain_HitsThreshold_GrantsOneShield` | SP=25, shields=0/2 + 10 → SP=5, shields=1 |
| 3 | `ApplySpGain_MultipleThresholds_GrantsMultipleShields` | SP=0, shields=0/2 + 65 → SP=5, shields=2 |
| 4 | `ApplySpGain_AtMaxShieldCap_CapsSpAt30_NoConversion` | SP=25, shields=2/2 + 10 → SP=30, shields=2 (TDD invariant 3) |
| 5 | `ApplySpGain_AboveThresholdAtMaxCap_CapsAt30NotHigher` | SP=0, shields=2/2 + 40 → SP=30, shields=2 |
| 6 | `ApplySpDeduction_ResultsInNegativeDeepDeficit` | SP=10, deduct 30 → SP=-20 (TDD invariant 1) |
| 7 | `ApplySpDeduction_Recovery_Requires20SpToReachZero` | SP=-20 + 20 → SP=0, shields unchanged, no conversion (TDD invariant 2) |

*Test 7 verifies the recovery using `ApplySpGain(-20, 0, 2, 20)` → `(0, 0)`: SP=0 (<30), no conversion.*

### D.2 — Update: `LogHabitProgressCommandTests.cs`

**File:** `tests/LifeGrid.Application.Tests/HabitLogging/LogHabitProgressCommandTests.cs`

**Existing tests:** Update the broadcaster mock assertion from `Received().Broadcast()` to `Received().BroadcastEconomy(Arg.Any<int>(), Arg.Any<int>())` wherever the existing "broadcaster called once" test fires. No assertion logic changes.

**New test (TDD invariant 4 — Atomic Consistency):**

```csharp
[Fact]
public async Task CommitFailure_BroadcasterNotCalled()
{
    // Arrange
    var habit = HabitEntity.Create(SeedWeekGoal.WeekGoalId, "Run", HabitType.Planned, 5.0, "km", null, null);
    _habitRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(habit);
    _uow.CommitAsync(Arg.Any<CancellationToken>()).Throws(new InvalidOperationException("DB failure"));

    // Act
    var act = async () => await _handler.Handle(
        new LogHabitProgressCommand(habit.HabitId, 3.0, null, null), default);

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>();
    _broadcaster.DidNotReceive().BroadcastEconomy(Arg.Any<int>(), Arg.Any<int>());
}
```

---

## Implementation Notes

- `GrantShield()` on `UserEconomy` is **not removed** — still used by `GrantSurveyBonusShield()` (bonus cap-bypass logic). The change is solely that `GrantSp` no longer calls it.
- All four ViewModel WeakReferenceMessenger registrations use `async (r, _) => await r.LoadAsync()` — the `_` ignores the message payload, so the enriched `EconomyStateMutatedMessage` requires no subscriber changes.
- `Color` type (`Microsoft.Maui.Graphics.Color`) is available in `LifeGrid.Presentation` without adding new NuGet references.
- `#CACACA` used as `NormalSpColor` matches the existing `OnSurface` resource in light mode. For full consistency, this could be read from `Application.Current.Resources` at runtime, but a hardcoded constant avoids MAUI App lifecycle coupling in the ViewModel.

---

## Acceptance Criteria

- `dotnet build LifeGrid.slnx` → 0 errors, ≤ 15 warnings (all pre-existing).
- `dotnet test` → **331 tests pass** (Domain: 106, Application: 157, Infrastructure: 68).
- Logging a habit earning 10 SP from `SP = 25` → `SP = 5`, `Shields = 1` (conversion fires at 30).
- Logging a habit earning 10 SP when `SP = 25` and `Shields = 2/2` → `SP = 30`, `Shields = 2` (capped, not converted).
- Applying a `-30 SP` deduction on a profile with `SP = 10` → `SP = -20`.
- HUD SP display turns `#FF1B77` (magenta/error) when `SP < 0`.
- HUD SP display returns to normal color after recovery (any logging that brings SP ≥ 0).
- A profile at `SP = -20` earning `20 SP` reaches `SP = 0` with no shield granted.
- Freeze consuming a shield broadcasts via `BroadcastEconomy`; Hibernate still uses `Broadcast()`.
- No EF migration required.
