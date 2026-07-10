# Phase 31 Plan: Procrastination & Underachievement Engine (Weekly Escalator)

**Status:** DONE ✓  
**Completed:** 2026-06-28  
**Requirements source:** `docs/requirements/Phase-31-requirements.md`  
**Finalized requirements:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` (P31.1–P31.12)  
**Test result:** 383 tests passing — 0 failures (121 Domain / 188 Application / 74 Infrastructure)

---

## Implementation Notes (Deviations from Plan)

| Area | Plan | Actual |
|---|---|---|
| P31.6 `ShieldsAvailable` | Only `GetWeeklyHabitsQueryHandler` updated | **Both** `GetWeeklyHabitsQueryHandler` and `GetCurrentWeekHabitsQueryHandler` updated — both construct `WeeklyHabitsDashboardDto` |
| P31.9 shield button visibility | `MultiBinding` or `IValueConverter` | `CanUseShield` computed property on `WeeklyGoalGroupItem` (takes `hasShields` ctor param); consistent with existing `isLoggingEnabled` pattern |
| P31.9 XAML command binding names (post-completion fix) | Plan used `UseShieldAsyncCommand` etc. | CommunityToolkit.MVVM 8.4.2 strips `Async` suffix — `CloseWeekAsyncCommand` → `CloseWeekCommand`, `GoToSummaryAsyncCommand` → `GoToSummaryCommand`, `UseShieldAsyncCommand` → `UseShieldCommand` in both XAML pages |

---

## Clarifications Recorded (2026-06-28)

| # | Question | Answer |
|---|---|---|
| 1 | `IsSystemReckoningLockdown` storage | Derive from `Goal.Status == Overwhelmed` at runtime — no new column |
| 2 | "Fix with Shield" target | Resets CLOSED week's WeekGoal PenaltyState `Level1Warning → Clean`; appears on WeekSummary + WeeklyHabitsPage |
| 3 | Reckoning lockdown route | Existing `overwhelmed-recalculate?goalId={id}` — no new page |
| 4 | Navigation lockdown enforcement | Not enforced in Phase 31; auto-navigate only |
| 5 | Previous penalty state lookup | New `GetPreviousWeekGoalAsync(goalId, currentWeekNumber)` on `IWeekRepository`; null → `Clean` |

---

## Pre-Flight Architecture Notes

- `PenaltyState` enum, `GoalStatus.Overwhelmed`, `Result<T>`, and `UserProfile.ConsumeShield()` all exist.
- `GetByIdAsync` already includes WeekGoals via EF `Include` — no repository changes needed for load.
- `PenaltyState.HasConversion<string>()` serialises as C# enum names (`"Level1Warning"` not `"Level_1_Warning"`). Pre-existing bug in `WeeklyGoalGroupItem.IsInPenalty` must be fixed in Phase D.
- `CloseWeekCommand` return type changes from `Result` → `Result<CloseWeekCommandResult>`. All callers updated.
- Single `CommitAsync` at end of `CloseWeekCommandHandler` persists all mutations atomically (EF change tracking).
- No EF migration required — all mutated columns already mapped.

---

## Implementation Phases

---

### Phase A: Domain Layer

**A1. `WeekGoal` — add mutation methods**
- File: `src/LifeGrid.Domain/WeekGoal/WeekGoal.cs`
- Add:
  ```csharp
  public void SetPenaltyState(PenaltyState state) => PenaltyState = state;
  public void ApplyXpPenalty(int penalizedXp)     => GoalWeeklyXpEarned = penalizedXp;
  ```
  Both are `public` (Application layer must call them; Domain Service pattern).

**A2. `Goal` — add `MarkOverwhelmed()`**
- File: `src/LifeGrid.Domain/Goal/Goal.cs`
- Add:
  ```csharp
  public void MarkOverwhelmed() => Status = GoalStatus.Overwhelmed;
  ```

**A3. `EscalationResult` record + `ProcrastinationEscalationEngine` static class**
- File: `src/LifeGrid.Domain/Gamification/ProcrastinationEscalationEngine.cs`
- `EscalationResult` record:
  ```csharp
  public record EscalationResult(PenaltyState NewPenaltyState, int PenalizedXp, bool TriggersOverwhelmed);
  ```
- `ProcrastinationEscalationEngine.Evaluate(PenaltyState currentState, double goalWeeklyGp, int currentXpEarned)`:

  ```
  Clean:
    GP <= 80.0 → Level1Warning, XP unchanged, overwhelmed=false
    GP >  80.0 → Clean,         XP unchanged, overwhelmed=false

  Level1Warning:
    GP == 100.0 → Clean,          XP unchanged,                 overwhelmed=false
    GP <  100.0 → ProbationWeek2, XP = (int)Math.Floor(xp/2.0), overwhelmed=false

  ProbationWeek2:
    GP == 100.0 → Clean,           XP unchanged, overwhelmed=false
    GP <  100.0 → ReckoningWeek3,  XP = 0,       overwhelmed=true
  ```

  100% clearance uses `== 100.0` exact comparison (double equality is safe here because `GoalWeeklyGp` is capped via `Math.Min(..., 100.0)`).

---

### Phase B: Application Layer

**B1. `IWeekRepository` — new method**
- File: `src/LifeGrid.Application/Week/IWeekRepository.cs`
- Add:
  ```csharp
  Task<WeekGoalEntity?> GetPreviousWeekGoalAsync(
      Guid goalId, int currentWeekNumber, CancellationToken ct = default);
  ```

**B2. `CloseWeekCommandResult` record + updated `CloseWeekCommand`**
- File: `src/LifeGrid.Application/Week/CloseWeekCommand.cs`
- Add result record (same file):
  ```csharp
  public record CloseWeekCommandResult(Guid? OverwhelmedGoalId);
  ```
- Change handler return type: `IRequestHandler<CloseWeekCommand, Result<CloseWeekCommandResult>>`
- Add `IGoalRepository goalRepository` to constructor
- Updated handler body:
  1. Load week — return `Result<CloseWeekCommandResult>.Failure("week_not_found")` if null
  2. Declare `Guid? overwhelmedGoalId = null`
  3. For each `weekGoal` in `week.WeekGoals`:
     - `var prev = await weekRepository.GetPreviousWeekGoalAsync(weekGoal.GoalId, week.WeekNumber, ct)`
     - `var prevState = prev?.PenaltyState ?? PenaltyState.Clean`
     - `var esc = ProcrastinationEscalationEngine.Evaluate(prevState, weekGoal.GoalWeeklyGp, weekGoal.GoalWeeklyXpEarned)`
     - `weekGoal.SetPenaltyState(esc.NewPenaltyState)`
     - `weekGoal.ApplyXpPenalty(esc.PenalizedXp)`
     - If `esc.TriggersOverwhelmed`:
       - `var goal = await goalRepository.GetByIdAsync(weekGoal.GoalId, ct)`
       - `goal?.MarkOverwhelmed()`
       - `overwhelmedGoalId = weekGoal.GoalId`
  4. `week.Close()`
  5. `await unitOfWork.CommitAsync(ct)`
  6. `broadcaster.Broadcast()`
  7. Return `Result<CloseWeekCommandResult>.Success(new(overwhelmedGoalId))`

**B3. `UseShieldCommand` — new command**
- File: `src/LifeGrid.Application/WeekGoal/UseShieldCommand.cs`
- Command: `record UseShieldCommand(Guid WeekGoalId) : IRequest<Result>`
- Handler constructor: `IWeekRepository weekRepository, IUserProfileRepository profileRepository, IUnitOfWork unitOfWork, IEconomyStateBroadcaster broadcaster`
- Handler body (per requirements P31.4)

**B4. Update `WeekLifecycleSyncService`**
- File: `src/LifeGrid.Application/Week/WeekLifecycleSyncService.cs`
- In `HandleWednesdayAsync`: change `var result = await _mediator.Send(new CloseWeekCommand(...))` — the return is now `Result<CloseWeekCommandResult>`. The sync service checks `result.IsSuccess` only; `OverwhelmedGoalId` is ignored.

**B5. `WeeklyHabitsDashboardDto` — add `ShieldsAvailable`**
- File: `src/LifeGrid.Application/WeeklyHabits/WeeklyHabitsDashboardDto.cs`
- Add `int ShieldsAvailable` parameter to the record

**B6. `GetWeeklyHabitsQueryHandler` — load UserProfile**
- File: `src/LifeGrid.Application/WeeklyHabits/GetWeeklyHabitsQueryHandler.cs`
- Add `IUserProfileRepository profileRepository` to constructor
- Load `var profile = await profileRepository.GetSingleAsync(cancellationToken)`
- Pass `profile?.Economy.ShieldsAvailable ?? 0` to `WeeklyHabitsDashboardDto`

---

### Phase C: Infrastructure Layer

**C1. `WeekRepository.GetPreviousWeekGoalAsync`**
- File: `src/LifeGrid.Infrastructure/Data/Repositories/WeekRepository.cs`
- Implementation: find the most recent week before `currentWeekNumber` that contains a WeekGoal for `goalId`:
  ```csharp
  public async Task<WeekGoalEntity?> GetPreviousWeekGoalAsync(
      Guid goalId, int currentWeekNumber, CancellationToken ct = default)
  {
      var prevWeek = await db.Weeks
          .Where(w => w.WeekNumber < currentWeekNumber &&
                      w.WeekGoals.Any(wg => wg.GoalId == goalId))
          .OrderByDescending(w => w.WeekNumber)
          .Include(w => w.WeekGoals)
          .FirstOrDefaultAsync(ct);

      return prevWeek?.WeekGoals.FirstOrDefault(wg => wg.GoalId == goalId);
  }
  ```
  Returns `null` when the goal has never appeared in a prior week.

---

### Phase D: Presentation Layer

**D1. `WeeklyGoalGroupItem` — fix bug + add properties**
- File: `src/LifeGrid.Presentation/ViewModels/WeeklyGoalGroupItem.cs`
- Fix `IsInPenalty`: change from underscore strings to `dto.PenaltyState != "Clean"`
- Add `GoalId`:
  ```csharp
  GoalId = dto.GoalId;
  ```
- Add `IsLevel1Warning`:
  ```csharp
  IsLevel1Warning = dto.PenaltyState == "Level1Warning";
  ```
- Add `public Guid GoalId { get; }` and `public bool IsLevel1Warning { get; }` to properties

**D2. `WeeklyHabitsViewModel` — shield + close updates**
- File: `src/LifeGrid.Presentation/ViewModels/WeeklyHabitsViewModel.cs`
- Add `[ObservableProperty] private bool _hasShieldsAvailable;`
- In `LoadAsync()`: `HasShieldsAvailable = dto.ShieldsAvailable > 0;`
- Add `UseShieldAsync(WeeklyGoalGroupItem item)` `[RelayCommand]`:
  ```csharp
  var result = await _mediator.Send(new UseShieldCommand(item.WeekGoalId));
  if (result.IsSuccess) await LoadAsync();
  ```
- Update `CloseWeekAsync()`:
  ```csharp
  var result = await _mediator.Send(new CloseWeekCommand(_weekId));
  if (!result.IsSuccess) return;
  if (result.Value?.OverwhelmedGoalId is { } gid)
      await Shell.Current.GoToAsync($"overwhelmed-recalculate?goalId={gid}");
  else
      await LoadAsync();
  ```

**D3. `WeekSummaryViewModel` — shield support**
- File: `src/LifeGrid.Presentation/ViewModels/WeekSummaryViewModel.cs`
- Add `[ObservableProperty] private bool _hasShieldsAvailable;`
- In `LoadAsync()`: `HasShieldsAvailable = dto.ShieldsAvailable > 0;`
- Add `UseShieldAsync(WeeklyGoalGroupItem item)` `[RelayCommand]` — same as above

**D4. `WeeklyHabitsPage.xaml` — warning flag + shield button**
- File: `src/LifeGrid.Presentation/Pages/WeeklyHabitsPage.xaml`
- In the goal group header area, adjacent to `GoalDescription`:
  - Add `Label` with Material Symbol `warning` glyph, `TextColor="{StaticResource Error}"`, `IsVisible="{Binding IsInPenalty}"`
- After the header row, add "FIX WITH SHIELD" `Button`:
  - `Command="{Binding Source={x:Reference PageRoot}, Path=BindingContext.UseShieldAsyncCommand}"`
  - `CommandParameter="{Binding .}"` (the `WeeklyGoalGroupItem`)
  - Visibility requires both `IsLevel1Warning` (on item) AND `HasShieldsAvailable` (on VM) — use a `MultiBinding` with `BooleanAndConverter`, or use a `DataTrigger` approach
  - Style: `BackgroundColor="{StaticResource Error}"`, `TextColor="{StaticResource OnPrimary}"`, `CornerRadius="2"`, `Text="FIX WITH SHIELD"`

**D5. `WeekSummaryPage.xaml` — same warning flag + shield button**
- File: `src/LifeGrid.Presentation/Pages/WeekSummaryPage.xaml`
- Identical additions as D4

---

### Phase E: Tests

**E1. Domain — `ProcrastinationEscalationEngineTests.cs`**
- File: `tests/LifeGrid.Domain.Tests/Gamification/ProcrastinationEscalationEngineTests.cs`
- 8 tests covering all state transitions and boundary values (see P31.10 in FUNCTIONAL_REQUIREMENTS.md)

**E2. Application — `CloseWeekCommandTests.cs` additions**
- File: `tests/LifeGrid.Application.Tests/Week/CloseWeekCommandTests.cs`
- 4 new tests: `Clean_Below80`, `Level1Warning_Below100`, `Probation_Below100_MarksOverwhelmed`, `NoPreviousWeek_TreatedAsClean`
- Mock: `IWeekRepository.GetPreviousWeekGoalAsync` + `IGoalRepository.GetByIdAsync`

**E3. Application — `UseShieldCommandTests.cs` (new)**
- File: `tests/LifeGrid.Application.Tests/WeekGoal/UseShieldCommandTests.cs`
- 4 tests: shield consumed, no shields, not Level1Warning, weekgoal not found

**E4. Infrastructure — `WeekRepositoryTests.cs` additions**
- File: `tests/LifeGrid.Infrastructure.Tests/Repositories/WeekRepositoryTests.cs`
- 2 tests: `GetPreviousWeekGoal_HasPrevious_Returns`, `GetPreviousWeekGoal_NoPrevious_ReturnsNull`

---

## Estimated Test Count

| Layer | Before | New | After |
|---|---|---|---|
| Domain | 113 | 8 | 121 |
| Application | 180 | 8 | 188 |
| Infrastructure | 72 | 2 | 74 |
| **Total** | **365** | **18** | **383** |

---

## EF Migration

No migration required. `PenaltyState` → `HasConversion<string>()` (no schema change). `GoalStatus.Overwhelmed` → already mapped. `WeekGoal.SetPenaltyState` / `ApplyXpPenalty` mutate columns that exist.

---

## Risk & Open Questions

| Risk | Mitigation |
|---|---|
| `MultiBinding` / `BooleanAndConverter` not available in MAUI XAML out-of-box | Use a computed property `CanUseShield` on `WeeklyGoalGroupItem` that takes `hasShields` as a constructor param; simpler than MultiBinding |
| `double == 100.0` exact equality for 100% GP clearance | `GoalWeeklyGp` is set by `GamificationCalculationEngine.CalculateWeekGoalGp` which uses `Math.Min(..., 100.0)` — only reaches exactly 100.0 when fully complete; safe |
| `overwhelmedGoalId` set to last overwhelmed goal if multiple goals overwhelm in one close | Phase 31 scope: only one goal can trigger Reckoning per close (by the 3-week ladder, only one goal should be at `ProbationWeek2` at a time for a fresh start). If multiple, last-wins is acceptable for Phase 31. |
