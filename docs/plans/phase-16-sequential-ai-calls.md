# Phase 16 — Sequential AI Calls with Blueprint Cache
**Status:** DONE (implemented 2026-06-22)

> **Post-implementation note:** The `CachedGoalId` field described in this plan was superseded during Phase 17 (`PendingGoalId` added) and Phase 18 (both merged into a single `GoalId` field with delete-on-completion semantics). The `BlueprintJson` field and two-command split (`GenerateBlueprintCommand` / `GenerateScheduleCommand`) are unchanged. See `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` §Phase 17 and §Phase 18 for the final state.

## Source Documents
- Requirements: `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` §P16
- Prompt files: `src/LifeGrid.Infrastructure/AI/Prompts/prompt2.1.txt`, `prompt2.2.txt`
- Impacted files: `GenerateHabitsCommand.cs`, `GeminiHabitGenerationService.cs`, `OnboardingSession.cs`, `CreateGoalViewModel` (SetupViewModel.cs), `SetupPage.xaml`

---

## Clarifications Confirmed (2026-06-22)

| Question | Answer |
|---|---|
| Cache storage for blueprint | Add `BlueprintJson` field to `OnboardingSession` with a new EF migration; blueprint is keyed to `GoalId` (see Phase 18 — `CachedGoalId` was merged into `GoalId`) |
| Loading state UI | Two separate bool flags: keep `IsGeneratingHabits` (Phase 1), add `IsGeneratingSchedule` (Phase 2) |
| "Open/close connection" meaning | Sequential calls + intermediate DB save between them — no explicit HTTP client disposal |

---

## Architecture Overview

### New Types
- `BlueprintResult` (discriminated union) — the return value of prompt 2.1: either `Feasible(string BlueprintJson)` or `Infeasible(...)`.

### Interface Change (`IGeminiHabitGenerationService`)
Two new methods are added. The existing `GenerateScheduleAsync` (used by `RecalculateGoalScheduleCommand`) is **unchanged**.

```
GenerateBlueprintAsync(...)   → Result<BlueprintResult>          // prompt 2.1 only
GenerateScheduleFromBlueprintAsync(blueprintJson, startDate, ...) → Result<HabitSchedulingResult>  // prompt 2.2 only
GenerateScheduleAsync(...)    → Result<HabitSchedulingResult>    // existing, unchanged (Overwhelmed path)
```

### New Application Commands
`GenerateHabitsCommand` is retired. Two new commands replace it:

| Command | Does |
|---|---|
| `GenerateBlueprintCommand` | Checks session cache → calls prompt 2.1 if miss → saves `BlueprintJson` + `CachedGoalId` to session |
| `GenerateScheduleCommand` | Reads `BlueprintJson` from session → calls prompt 2.2 → saves Weeks/WeekGoals/Habits |

### ViewModel Changes (`CreateGoalViewModel`)
`AutoResumeHabitGenerationAsync` is split:
1. `IsGeneratingHabits = true` → send `GenerateBlueprintCommand` → `IsGeneratingHabits = false`
2. If feasible: `IsGeneratingSchedule = true` → send `GenerateScheduleCommand` → `IsGeneratingSchedule = false`

### DB Change
New columns on `OnboardingProgressCache` table: `BlueprintJson` (TEXT, nullable), `CachedGoalId` (BLOB/GUID, nullable).

---

## Implementation Plan

### Phase A — TDD Stub (Write failing tests first)

**File:** `tests/LifeGrid.Application.Tests/Habit/GenerateBlueprintCommandTests.cs` (new)

Tests to write (all failing until Phase D):
- `NoActiveSession_ReturnsFailure`
- `SessionNotInExecutionVerified_ReturnsFailure`
- `NoUserProfile_ReturnsFailure`
- `GoalNotFound_ReturnsFailure`
- `CacheHit_ReturnsBlueprintReadyWithoutCallingAI` — session has `BlueprintJson` set and `CachedGoalId == goal.GoalId` → `_aiService.GenerateBlueprintAsync` is NOT called, result is `HabitGenerationOutcome.Complete`
- `CacheMiss_CallsAIAndCachesBlueprint` — blank session → AI called → session upserted with blueprint
- `AIReturnsInfeasible_ReturnsInfeasibleOutcome` — blueprint call returns infeasible
- `AIFailure_ReturnsFailureResult`

**File:** `tests/LifeGrid.Application.Tests/Habit/GenerateScheduleCommandTests.cs` (rename/update from `GenerateHabitsCommandTests.cs`)

The existing tests in `GenerateHabitsCommandTests.cs` are updated to test `GenerateScheduleCommandHandler`. Changes:
- The handler now reads `session.BlueprintJson` instead of calling `GenerateScheduleAsync`; mock the session helper `SessionAtExecutionVerified()` must call `session.CacheBlueprint(goalId, "{}")` to set a valid cache.
- Replace `_aiService.GenerateScheduleAsync(...)` mocks with `_aiService.GenerateScheduleFromBlueprintAsync(...)` mocks.
- All DB persistence tests (week, habit, UoW commit, shield bonus, deduplication) remain; only the AI mock changes.

> **Note:** `GenerateBlueprintAsync` and `GenerateScheduleFromBlueprintAsync` are new interface methods. Add them to the mock substitution in both test files.

---

### Phase B — Domain: `OnboardingSession.cs`

**File:** `src/LifeGrid.Domain/Onboarding/OnboardingSession.cs`

**Add properties:**
```csharp
public string? BlueprintJson  { get; private set; }
public Guid?   CachedGoalId   { get; private set; }
```

**Add method:**
```csharp
public void CacheBlueprint(Guid goalId, string blueprintJson)
{
    CachedGoalId  = goalId;
    BlueprintJson = blueprintJson;
    LastActiveTimestamp = DateTime.UtcNow;
}
```

**Update `Reset()`:** add `BlueprintJson = null; CachedGoalId = null;`.

> `AdvanceToExecutionVerified()` does NOT need to clear `BlueprintJson` — the blueprint is cached after `ExecutionVerified` is set, and must survive across retry attempts.

---

### Phase C — Application: New Type + Updated Interface

**File:** `src/LifeGrid.Application/Week/BlueprintResult.cs` (new)
```csharp
namespace LifeGrid.Application.Week;

public abstract record BlueprintResult
{
    public sealed record Feasible(string BlueprintJson) : BlueprintResult;

    public sealed record Infeasible(
        string  Reason,
        string? SuggestedDeadline,
        string? SuggestedScope) : BlueprintResult;
}
```

**File:** `src/LifeGrid.Application/Week/IGeminiHabitGenerationService.cs`

Add two new methods (keep existing `GenerateScheduleAsync`):
```csharp
Task<Result<BlueprintResult>> GenerateBlueprintAsync(
    string            goalAsStated,
    string            deadlineAsStated,
    string            baselineAnswersJson,
    DateTime          startDate,
    CancellationToken ct = default);

Task<Result<HabitSchedulingResult>> GenerateScheduleFromBlueprintAsync(
    string            blueprintJson,
    DateTime          startDate,
    CancellationToken ct = default);
```

---

### Phase D — Application: New Commands

**File:** `src/LifeGrid.Application/Week/Commands/GenerateBlueprintCommand.cs` (new)

```
public record GenerateBlueprintCommand : IRequest<Result<HabitGenerationOutcome>>;

Handler dependencies:
  IOnboardingRepository, IUserProfileRepository, IGoalRepository,
  IGeminiHabitGenerationService

Logic:
  1. GetActiveSessionAsync → fail if null or not ExecutionVerified
  2. GetSingleAsync (UserProfile) → fail if null
  3. GetByUserIdAsync (Goal) → fail if null
  4. Cache hit check:
       if (session.BlueprintJson != null && session.CachedGoalId == goal.GoalId)
           return HabitGenerationOutcome.Complete   // skip AI
  5. Build baselineAnswersJson from goal.RefinementAnswers (same helper as current GenerateHabitsCommandHandler)
  6. aiService.GenerateBlueprintAsync(goal.Description, goal.DeadlineDate.ToString("yyyy-MM-dd"),
         baselineAnswersJson, session.ChosenStartDate!.Value, ct)
     → fail if Result.IsSuccess == false
  7. If BlueprintResult.Infeasible → return HabitGenerationOutcome.Infeasible(...)
  8. session.CacheBlueprint(goal.GoalId, ((BlueprintResult.Feasible)result).BlueprintJson)
  9. UpsertAsync(session)
  10. return HabitGenerationOutcome.Complete
```

**File:** `src/LifeGrid.Application/Week/Commands/GenerateScheduleCommand.cs` (new — replaces `GenerateHabitsCommand.cs`)

```
public record GenerateScheduleCommand : IRequest<Result<HabitGenerationOutcome>>;

Handler dependencies: same as GenerateHabitsCommandHandler today

Logic:
  1. GetActiveSessionAsync → fail if null or not ExecutionVerified
  2. GetSingleAsync (UserProfile) → fail if null
  3. GetByUserIdAsync (Goal) → fail if null
  4. Verify session.BlueprintJson != null && session.CachedGoalId == goal.GoalId
     → fail with "No blueprint cached for this goal." if check fails
  5. aiService.GenerateScheduleFromBlueprintAsync(session.BlueprintJson!, session.ChosenStartDate!.Value, ct)
  6. If result not success → fail
  7. HabitSchedulingResult.Infeasible → return HabitGenerationOutcome.Infeasible (should not happen
     at this stage but handle defensively)
  8. HabitSchedulingResult.Feasible → persist weeks/habits (IDENTICAL to current GenerateHabitsCommandHandler
     loop: AddAsync / AddWeekGoalAsync, AddRangeAsync per week)
  9. isFirstGoal shield bonus (unchanged)
  10. unitOfWork.CommitAsync
  11. session.AdvanceToHabitsGenerated()
  12. UpsertAsync(session)
  13. return HabitGenerationOutcome.Complete
```

**File:** `src/LifeGrid.Application/Week/Commands/GenerateHabitsCommand.cs` (DELETE — replaced by above two files)

---

### Phase E — Infrastructure

#### E1: `GeminiHabitGenerationService.cs`

Implement the two new interface methods:

**`GenerateBlueprintAsync`**: executes only the prompt 2.1 call (current "Call 1" block). Returns `Result<BlueprintResult>`:
- On HTTP failure → `Result<BlueprintResult>.Failure(...)`
- On JSON parse of `isFeasible=false` → `Result<BlueprintResult>.Success(new BlueprintResult.Infeasible(...))`
- On `isFeasible=true` → `Result<BlueprintResult>.Success(new BlueprintResult.Feasible(call1Raw))`

The raw JSON string (`call1Raw`) is returned as-is in `BlueprintResult.Feasible.BlueprintJson` — no re-serialization.

**`GenerateScheduleFromBlueprintAsync(string blueprintJson, DateTime startDate, ...)`**: executes only the prompt 2.2 call (current "Call 2" block). Takes `blueprintJson` directly (replaces `call1Raw`). Returns `Result<HabitSchedulingResult>` via existing `ParseSchedule`.

`GenerateScheduleAsync` (existing combined method) **is unchanged**. The infeasibility return path inside it remains as-is (used by `RecalculateGoalScheduleCommand`).

#### E2: `OnboardingSessionConfiguration.cs`

Add two property mappings:
```csharp
builder.Property(e => e.BlueprintJson);   // nullable TEXT, no max length needed (raw AI JSON)
builder.Property(e => e.CachedGoalId);    // nullable Guid
```

#### E3: EF Migration

Run `dotnet ef migrations add Phase16_AddBlueprintCacheToSession --project src/LifeGrid.Infrastructure --startup-project src/LifeGrid.Presentation`

Expected migration: adds `BlueprintJson` (TEXT, nullable) and `CachedGoalId` (BLOB, nullable) columns to `OnboardingProgressCache`.

---

### Phase F — Presentation

#### F1: `SetupViewModel.cs` (CreateGoalViewModel)

```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(IsEntryFlowVisible))]
private bool _isGeneratingSchedule = false;
```

Also add `[NotifyPropertyChangedFor(nameof(IsEntryFlowVisible))]` to the existing `_isGeneratingHabits` field.

Update `IsEntryFlowVisible`:
```csharp
public bool IsEntryFlowVisible =>
    !IsGeneratingHabits && !IsGeneratingSchedule && string.IsNullOrEmpty(InfeasibilityReason);
```

Replace `AutoResumeHabitGenerationAsync` body:
```csharp
private async Task AutoResumeHabitGenerationAsync()
{
    // Phase 1: Blueprint (cache hit returns immediately)
    IsGeneratingHabits = true;
    var blueprintResult = await mediator.Send(new GenerateBlueprintCommand());
    IsGeneratingHabits = false;

    if (!blueprintResult.IsSuccess)
    {
        ValidationError = blueprintResult.Error ?? "Blueprint generation failed. Please try again.";
        return;
    }

    if (blueprintResult.Value is HabitGenerationOutcome.Infeasible infeasible)
    {
        IsRefinementActive = false;
        var hint = infeasible.SuggestedDeadline is not null
            ? $"\n\nSuggested deadline: {infeasible.SuggestedDeadline}"
            : string.Empty;
        InfeasibilityReason = infeasible.RecalibrationReason + hint;
        return;
    }

    // Phase 2: Schedule
    IsGeneratingSchedule = true;
    var scheduleResult = await mediator.Send(new GenerateScheduleCommand());
    IsGeneratingSchedule = false;

    if (!scheduleResult.IsSuccess)
    {
        ValidationError = scheduleResult.Error ?? "Schedule generation failed. Please try again.";
        return;
    }

    IsRefinementActive = false;
    appShellViewModel.SetOnboardingComplete();
    await Shell.Current.GoToAsync("//goals");
}
```

> Remove the old `switch (habitResult.Value)` block and the `HabitGenerationOutcome.Complete` / `HabitGenerationOutcome.Infeasible` handling from the old single-command path.

#### F2: `SetupPage.xaml`

After the existing STATE E block (`IsGeneratingHabits`), add STATE E2:
```xml
<!-- STATE E2: Generating schedule (prompt 2.2) -->
<VerticalStackLayout
    IsVisible="{Binding IsGeneratingSchedule}"
    Spacing="12"
    HorizontalOptions="Center">

    <ActivityIndicator
        IsRunning="{Binding IsGeneratingSchedule}"
        Color="{StaticResource Primary}"
        HorizontalOptions="Center" />

    <Label
        Text="Establishing the details of your schedule"
        HorizontalOptions="Center"
        HorizontalTextAlignment="Center" />

</VerticalStackLayout>
```

---

### Phase G — Build & Verify

```
dotnet build LifeGrid.slnx
dotnet test
```

Expected: 0 build errors; all prior 253 tests pass + new tests in `GenerateBlueprintCommandTests.cs` and updated tests in `GenerateScheduleCommandTests.cs`.

---

## Files Changed

| File | Change |
|---|---|
| `src/LifeGrid.Domain/Onboarding/OnboardingSession.cs` | Add `BlueprintJson`, `CachedGoalId`, `CacheBlueprint()`, update `Reset()` |
| `src/LifeGrid.Application/Week/BlueprintResult.cs` | **New** — `Feasible(BlueprintJson)` / `Infeasible(...)` discriminated union |
| `src/LifeGrid.Application/Week/IGeminiHabitGenerationService.cs` | Add `GenerateBlueprintAsync` + `GenerateScheduleFromBlueprintAsync`; keep `GenerateScheduleAsync` |
| `src/LifeGrid.Application/Week/Commands/GenerateBlueprintCommand.cs` | **New** — Phase 1 command with cache-hit logic |
| `src/LifeGrid.Application/Week/Commands/GenerateScheduleCommand.cs` | **New** — Phase 2 command (replaces `GenerateHabitsCommand`) |
| `src/LifeGrid.Application/Week/Commands/GenerateHabitsCommand.cs` | **Delete** |
| `src/LifeGrid.Infrastructure/AI/GeminiHabitGenerationService.cs` | Implement `GenerateBlueprintAsync` + `GenerateScheduleFromBlueprintAsync` |
| `src/LifeGrid.Infrastructure/Data/EntityConfigurations/OnboardingSessionConfiguration.cs` | Map `BlueprintJson` + `CachedGoalId` |
| `src/LifeGrid.Infrastructure/Migrations/Phase16_AddBlueprintCacheToSession.cs` | **New** — EF migration |
| `src/LifeGrid.Presentation/ViewModels/SetupViewModel.cs` | Add `IsGeneratingSchedule`, update `IsEntryFlowVisible`, rewrite `AutoResumeHabitGenerationAsync` |
| `src/LifeGrid.Presentation/Pages/SetupPage.xaml` | Add STATE E2 spinner block |
| `tests/LifeGrid.Application.Tests/Habit/GenerateBlueprintCommandTests.cs` | **New** — 8 tests |
| `tests/LifeGrid.Application.Tests/Habit/GenerateHabitsCommandTests.cs` | Rename to `GenerateScheduleCommandTests.cs`; update mocks to new interface |
