# Phase 32 Plan: Flash Quests & Temporal Multipliers

**Status:** DONE — 414 tests passing (130 Domain / 204 Application / 80 Infrastructure)
**Requirements source:** `docs/requirements/Phase-32-requirements.md`
**Finalized requirements:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` (P32.1–P32.11)

---

## Clarifications Recorded (2026-07-10)

| # | Question | Answer |
|---|---|---|
| 1 | `IsDoubleXpActive` storage | **Reuse existing `UserActiveStates.DoubleXpMode`/`DoubleXpExpiry`** (Phase 23 scaffold, matches `data-structure.json`). No new `Week`-level flag. Expiry = start of following week; self-expiring, no explicit deactivation needed. |
| 2 | Gemini call scope | **One batched call per week**, covering all lagging goals' habits together. |
| 3 | Quest → source attribution | **Extend `prompt8.txt`** with `habit_id` on input and required `source_habit_id` on output. |
| 4 | Countdown timer | **Live-ticking**, `IDispatcherTimer` refresh every 60s. |
| 5 | Duplicate injection guard | Skip the whole pipeline if the week already has any `Flash` habit. |
| 6 | Worker scheduling | New dedicated `ThursdayFlashQuestWorker` (separate from `WeekLifecycleSyncService`, which operates on the *previous* week — Flash Quests need the *current* week). |

---

## Pre-Flight Architecture Notes

- `HabitType.Flash` and `UserActiveStates.DoubleXpMode`/`DoubleXpExpiry` already exist in the domain (scaffolded ahead of need in Phases 4/23) — this phase activates dormant fields rather than adding new schema.
- `GamificationCalculationEngine.CalculateEntryReward` already treats Flash like any other habit type for base reward tiers; only a new `ApplyDoubleXp` post-processing step is needed.
- `GamificationCalculationEngine.CalculateWeekGoalGp` already includes Flash habits in GP (only `MomentBurst` is excluded) — no change required there.
- `WeekLifecycleSyncService.EvaluateAsync` resolves `previousMonday`'s week (the week that just ended) for its Monday/Wednesday handling. Flash Quests must act on `currentMonday`'s week (the one still in progress) — this is a different query, so it gets its own `IFlashQuestTriggerService` rather than a new branch on the existing service.
- `IHabitRepository.AddRangeAsync` and `GetCompletionSummariesForWeekGoalAsync` already exist and are reused as-is for habit injection and payload construction.
- `LogHabitProgressCommand`'s return type changes from `Result` to `Result<LogHabitProgressResult>` — this is a breaking change for `HabitLoggingViewModel` and existing `LogHabitProgressCommandTests`; both are updated in this phase.
- No EF migration required anywhere in this phase.

---

## Implementation Phases

---

### Phase A: Domain Layer

**A1. `Habit` — add deadline check**
- File: `src/LifeGrid.Domain/Habit/Habit.cs`
- Add:
  ```csharp
  public bool IsBeforeDeadline(DateTime at) => at <= DeadlineDateTime;
  ```

**A2. `UserActiveStates` — add activation + query methods**
- File: `src/LifeGrid.Domain/UserProfile/UserActiveStates.cs`
- Add:
  ```csharp
  internal void ActivateDoubleXp(DateTime expiry)
  {
      DoubleXpMode   = true;
      DoubleXpExpiry = expiry;
  }

  public bool IsDoubleXpActive(DateTime now) => DoubleXpMode && now < DoubleXpExpiry;
  ```

**A3. `UserProfile` — pass-throughs**
- File: `src/LifeGrid.Domain/UserProfile/UserProfile.cs`
- Add:
  ```csharp
  public void ActivateDoubleXp(DateTime expiry) => ActiveStates.ActivateDoubleXp(expiry);
  public bool IsDoubleXpActive(DateTime now)    => ActiveStates.IsDoubleXpActive(now);
  ```

**A4. `GamificationCalculationEngine` — multiplier**
- File: `src/LifeGrid.Domain/Gamification/GamificationCalculationEngine.cs`
- Add:
  ```csharp
  public static EntryReward ApplyDoubleXp(EntryReward reward, bool isDoubleXpActive)
      => isDoubleXpActive ? reward with { XpEarned = reward.XpEarned * 2 } : reward;
  ```
- Only `XpEarned` doubles; `SpEarned` is untouched.

---

### Phase B: Application Layer — Flash Quest Generation

**B1. New folder `src/LifeGrid.Application/FlashQuest/`**

**B1a. `GenerateFlashQuestCommand.cs`**
```csharp
public record GenerateFlashQuestCommand(Guid WeekId) : IRequest<Result<GenerateFlashQuestResult>>;
public record GenerateFlashQuestResult(int QuestsInjected);
```

**B1b. `GenerateFlashQuestCommandHandler.cs`**
- Dependencies: `IWeekRepository`, `IHabitRepository`, `IGoalRepository`, `IGeminiFlashQuestService`, `IDateTimeProvider`, `IUnitOfWork`.
- Logic:
  1. `week = await weekRepository.GetByIdAsync(request.WeekId, ct)` → `Failure("week_not_found")` if null.
  2. `if (await habitRepository.HasFlashHabitsInWeekAsync(week.WeekId, ct)) return Success(new(0))`.
  3. `laggingGoals = week.WeekGoals.Where(wg => wg.GoalWeeklyGp < 50.0).ToList()`; if empty → `Success(new(0))`.
  4. For each lagging goal: load habits (`GetByWeekGoalIdAsync`), load `Goal` (`goalRepository.GetByIdAsync`) for description context, load completion summaries (`GetCompletionSummariesForWeekGoalAsync`). Build `Dictionary<Guid HabitId, Guid WeekGoalId>` across all lagging goals.
  5. Serialize combined payload JSON: array of `{ habit_id, goal_description, habit_name, habit_description, habit_type, complete_measurement: { value }, target_measurement: { value, unit } }`.
  6. `var result = await geminiFlashQuestService.GenerateAsync(payloadJson, dateTimeProvider.UtcNow, ct)`.
  7. `if (!result.IsSuccess || result.Value is FlashQuestGenerationResult.NotEligible) return Success(new(0))`.
  8. `var accepted = (FlashQuestGenerationResult.Accepted)result.Value!;`
  9. For each `quest` in `accepted.Quests`: `if (!habitToWeekGoal.TryGetValue(quest.SourceHabitId, out var weekGoalId)) continue;` else build `Habit.Create(weekGoalId, HabitType.Flash, quest.QuestName, quest.Description, quest.MeasureValue, quest.MeasureUnit, dateTimeProvider.UtcNow.AddHours(24))`.
  10. `await habitRepository.AddRangeAsync(newHabits, ct)` (skip call if empty) → `await unitOfWork.CommitAsync(ct)` → `Success(new(newHabits.Count))`.

**B2. `IGeminiFlashQuestService.cs`**
```csharp
public interface IGeminiFlashQuestService
{
    Task<Result<FlashQuestGenerationResult>> GenerateAsync(
        string weeklyHabitsJson, DateTime currentDate, CancellationToken ct = default);
}
```

**B3. `FlashQuestGenerationResult.cs`**
```csharp
public abstract record FlashQuestGenerationResult
{
    public sealed record NotEligible : FlashQuestGenerationResult;
    public sealed record Accepted(IReadOnlyList<FlashQuestItem> Quests) : FlashQuestGenerationResult;
}

public record FlashQuestItem(
    Guid SourceHabitId, string QuestName, string Description, double MeasureValue, string MeasureUnit);
```

**B4. `IFlashQuestTriggerService.cs`**
```csharp
public interface IFlashQuestTriggerService
{
    Task EvaluateAsync(CancellationToken ct = default);
}
```

**B5. `FlashQuestTriggerService.cs`**
```csharp
public sealed class FlashQuestTriggerService(
    IWeekRepository weekRepository, ISender sender, IDateTimeProvider dateTimeProvider)
    : IFlashQuestTriggerService
{
    public async Task EvaluateAsync(CancellationToken ct = default)
    {
        var today = dateTimeProvider.UtcNow.Date;
        if (today.DayOfWeek != DayOfWeek.Thursday) return;

        int daysFromMon   = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var currentMonday = today.AddDays(-daysFromMon);
        var week = await weekRepository.GetByStartDateAsync(currentMonday, ct);
        if (week is null || week.Status != WeekStatus.Active) return;

        await sender.Send(new GenerateFlashQuestCommand(week.WeekId), ct);
    }
}
```
Mirrors the `currentMonday` computation used in `GetHudTelemetryQueryHandler` and `GetCurrentWeekHabitsQueryHandler`.

**B6. `IHabitRepository` — extend**
- File: `src/LifeGrid.Application/Habit/IHabitRepository.cs`
- Add: `Task<bool> HasFlashHabitsInWeekAsync(Guid weekId, CancellationToken ct = default);`

---

### Phase C: Application Layer — Double XP Wiring

**C1. `LogHabitProgressCommand` — result type**
- File: `src/LifeGrid.Application/HabitLogging/LogHabitProgressCommand.cs`
- Change to: `IRequest<Result<LogHabitProgressResult>>`
- Add (same file): `public record LogHabitProgressResult(int XpEarned, bool WasDoubled);`

**C2. `LogHabitProgressCommandHandler` — multiplier integration**
- File: `src/LifeGrid.Application/HabitLogging/LogHabitProgressCommandHandler.cs`
- Change `IRequestHandler<LogHabitProgressCommand, Result>` → `IRequestHandler<LogHabitProgressCommand, Result<LogHabitProgressResult>>`.
- Update all early-return `Result.Failure(...)` calls to `Result<LogHabitProgressResult>.Failure(...)`.
- After the existing `var reward = GamificationCalculationEngine.CalculateEntryReward(...)` line, insert:
  ```csharp
  if (habit.HabitType == HabitType.Flash
      && habit.IsBeforeDeadline(dateTimeProvider.UtcNow)
      && !profile.IsDoubleXpActive(dateTimeProvider.UtcNow))
  {
      profile.ActivateDoubleXp(week.StartDate.AddDays(7));
  }

  bool doubleXpActive = profile.IsDoubleXpActive(dateTimeProvider.UtcNow);
  reward = GamificationCalculationEngine.ApplyDoubleXp(reward, doubleXpActive);
  ```
  (Activation must precede the doubling check so the triggering Flash completion is itself doubled.)
- Final return: `Result<LogHabitProgressResult>.Success(new LogHabitProgressResult(reward.XpEarned, doubleXpActive))`.

---

### Phase D: Application Layer — HUD Extension

**D1. `HudTelemetryDto` — add flag**
- File: `src/LifeGrid.Application/Hud/HudTelemetryDto.cs`
- Add `bool IsDoubleXpActive` to the record.

**D2. `GetHudTelemetryQueryHandler` — populate flag**
- File: `src/LifeGrid.Application/Hud/GetHudTelemetryQuery.cs`
- All three DTO-construction branches (`profile is null`, `week is null`, happy path) pass `profile?.IsDoubleXpActive(dateTimeProvider.UtcNow) ?? false` (the no-profile branch passes `false` directly).

---

### Phase E: Infrastructure Layer

**E1. `HabitRepository.HasFlashHabitsInWeekAsync`**
- File: `src/LifeGrid.Infrastructure/Data/Repositories/HabitRepository.cs`
```csharp
public async Task<bool> HasFlashHabitsInWeekAsync(Guid weekId, CancellationToken ct = default)
    => await db.Habits
        .Join(db.WeekGoals, h => h.WeekGoalId, wg => wg.WeekGoalId, (h, wg) => new { h, wg })
        .AnyAsync(x => x.wg.WeekId == weekId && x.h.HabitType == HabitType.Flash, ct);
```

**E2. `prompt8.txt` — extend contract**
- Update `docs/specs/assets/prompts/prompt8.txt`:
  - Input `${WEEKLY_HABITS_JSON}` entries gain a `"habit_id"` field (documented in the prompt's input description).
  - Output schema: each object in `flash-quests` gains a required `"source_habit_id"` field, echoing the input `habit_id` it was generated from.
- Copy the updated prompt to `src/LifeGrid.Infrastructure/AI/Prompts/prompt8.txt` (embedded resource).
- Add to `LifeGrid.Infrastructure.csproj`: `<EmbeddedResource Include="AI\Prompts\prompt8.txt" />`.

**E3. `GeminiFlashQuestService.cs`**
- File: `src/LifeGrid.Infrastructure/AI/GeminiFlashQuestService.cs`
- Same shape as `GeminiMomentBurstService`: load embedded `prompt8.txt`, substitute `${CURRENT_DATE}` / `${WEEKLY_HABITS_JSON}`, call `IChatClient.GetResponseAsync`, `StripCodeFences`, then parse:
  - Raw text `== "N/A"` (trimmed) → `Result<FlashQuestGenerationResult>.Success(new FlashQuestGenerationResult.NotEligible())`.
  - Otherwise parse `{"flash-quests": [...]}`; for each element read `source_habit_id` (Guid), `falsh_quest_name` (existing prompt's typo — preserved for continuity), `habit_description`, `measure.value`, `measure.unit`. Wrap in `Accepted`.
  - `JsonException` / request failures → `Result<FlashQuestGenerationResult>.Failure(...)`.

**E4. DI registration**
- File: `src/LifeGrid.Infrastructure` DI extension (wherever `GeminiMomentBurstService` etc. are registered)
- Add: `services.AddScoped<IGeminiFlashQuestService, GeminiFlashQuestService>();`

---

### Phase F: Presentation — Background Scheduling

**F1. `WeekLifecycleScheduler.ComputeInitialDelay` — generalize hour**
- File: `src/LifeGrid.Presentation/Platforms/Android/WeekLifecycleScheduler.cs`
- Change signature to `ComputeInitialDelay(DayOfWeek targetDay, int targetHour = 9)`; replace hardcoded `9` (both in the `now.Hour >= 9` guard and `.AddHours(9)`) with `targetHour`.

**F2. `ThursdayFlashQuestWorker.cs`**
- File: `src/LifeGrid.Presentation/Platforms/Android/Workers/ThursdayFlashQuestWorker.cs`
- Same shape as `MondayWeekReminderWorker`/`WednesdayAutoCloseWorker`, resolving `IFlashQuestTriggerService` from a fresh DI scope and calling `EvaluateAsync()`.

**F3. `WeekLifecycleScheduler.Schedule()` — register third worker**
```csharp
private const string ThursdayWorkName = "lifegrid-thursday-flash-quest";
...
var thursdayRequest = BuildWeeklyRequest<Workers.ThursdayFlashQuestWorker>(DayOfWeek.Thursday, 12);
workManager.EnqueueUniquePeriodicWork(ThursdayWorkName, ExistingPeriodicWorkPolicy.Keep!, thursdayRequest);
```
(`BuildWeeklyRequest` also gets the `targetHour` parameter threaded through to `ComputeInitialDelay`.)

**F4. `MauiProgram.cs`**
- Register `builder.Services.AddScoped<IFlashQuestTriggerService, FlashQuestTriggerService>();`

---

### Phase G: Presentation — Flash Card Visualization & Live Countdown

**G1. `WeeklyHabitItem` — observable + countdown**
- File: `src/LifeGrid.Presentation/ViewModels/WeeklyHabitItem.cs`
- Convert from plain `sealed class` to `sealed partial class WeeklyHabitItem : ObservableObject`.
- Add `public bool IsFlash => HabitTypeLabel == "Flash";`
- Add `[ObservableProperty] private string _countdownText = string.Empty;`
- Add `public void RefreshCountdown(DateTime nowUtc)`:
  ```csharp
  var remaining = DeadlineDateTime - nowUtc;
  CountdownText = remaining <= TimeSpan.Zero
      ? "Expired"
      : $"Expires in {(int)remaining.TotalHours}h {remaining.Minutes}m";
  ```
- Call `RefreshCountdown(DateTime.UtcNow)` once at the end of the constructor (only meaningful for Flash items, but harmless for others).

**G2. `HomeViewModel` — live ticker**
- File: `src/LifeGrid.Presentation/ViewModels/HomeViewModel.cs`
- Add `private IDispatcherTimer? _countdownTimer;`
- In `LoadAsync()`, after populating `GoalGroups`, lazily start the timer if not already running:
  ```csharp
  _countdownTimer ??= Application.Current!.Dispatcher.CreateTimer();
  if (!_countdownTimer.IsRunning)
  {
      _countdownTimer.Interval = TimeSpan.FromSeconds(60);
      _countdownTimer.Tick += (_, _) => RefreshFlashCountdowns();
      _countdownTimer.Start();
  }
  ```
- Add `private void RefreshFlashCountdowns() { var now = DateTime.UtcNow; foreach (var h in GoalGroups.SelectMany(g => g.Habits).Where(h => h.IsFlash)) h.RefreshCountdown(now); }`

**G3. `WeeklyHabitsViewModel` — same live ticker**
- File: `src/LifeGrid.Presentation/ViewModels/WeeklyHabitsViewModel.cs`
- Identical addition to G2.

**G4. `HomePage.xaml` / `WeeklyHabitsPage.xaml` / `WeekSummaryPage.xaml` — Flash card styling**
- On the habit card template, add a `DataTrigger`/`IsVisible="{Binding IsFlash}"` treatment:
  - `Border` with `Stroke="{StaticResource Secondary}"` (`#e5cde1`), `StrokeThickness="2"`, `TextColor`/icon `{StaticResource OnSecondary}` (`#a20ba0`), `CornerRadius="2"`.
  - Material Symbol `local_fire_department` glyph label.
  - `<Label Text="{Binding CountdownText}" />`.

**G5. `HudView.xaml` — "2x XP ACTIVE" badge**
- File: `src/LifeGrid.Presentation/Controls/HudView.xaml`
- Add to the center `HorizontalStackLayout` (Grid.Column="1"), gated `IsVisible="{Binding IsDoubleXpActive}"`:
  ```xaml
  <Border BackgroundColor="{StaticResource Secondary}" StrokeThickness="0" Padding="6,2"
          IsVisible="{Binding IsDoubleXpActive}">
      <Border.StrokeShape><RoundRectangle CornerRadius="2" /></Border.StrokeShape>
      <Label Text="2x XP ACTIVE" TextColor="{StaticResource OnSecondary}"
             FontFamily="ShareTechMono" FontAttributes="Bold" FontSize="11" />
  </Border>
  ```

**G6. `HudViewModel` — bind flag**
- File: `src/LifeGrid.Presentation/ViewModels/HudViewModel.cs`
- Add `[ObservableProperty] private bool _isDoubleXpActive;`
- In `LoadAsync()`, inside the `MainThread.BeginInvokeOnMainThread` block: `IsDoubleXpActive = d.IsDoubleXpActive;`

**G7. `HabitLoggingViewModel` — "+XP (x2)" feedback**
- File: `src/LifeGrid.Presentation/ViewModels/HabitLoggingViewModel.cs`
- Inject `IToastNotificationService` via constructor (currently only `IMediator`).
- After `var result = await mediator.Send(...)`, on `result.IsSuccess`:
  ```csharp
  if (result.Value!.WasDoubled)
      await toastService.ShowInfoAsync("Double XP!", $"+{result.Value.XpEarned} XP (x2)");
  await Shell.Current.GoToAsync("..");
  ```

---

### Phase H: Tests

**H1. Domain**
- `tests/LifeGrid.Domain.Tests/Habit/HabitDeadlineTests.cs`
  - `IsBeforeDeadline_BeforeDeadline_ReturnsTrue`
  - `IsBeforeDeadline_AfterDeadline_ReturnsFalse`
- `tests/LifeGrid.Domain.Tests/UserProfile/UserActiveStatesDoubleXpTests.cs`
  - `ActivateDoubleXp_SetsModeAndExpiry`
  - `IsDoubleXpActive_BeforeExpiry_ReturnsTrue`
  - `IsDoubleXpActive_AfterExpiry_ReturnsFalse`
  - `IsDoubleXpActive_NeverActivated_ReturnsFalse`
- `tests/LifeGrid.Domain.Tests/Gamification/GamificationCalculationEngineTests.cs` (extend)
  - `ApplyDoubleXp_Active_DoublesXpOnly`
  - `ApplyDoubleXp_Inactive_ReturnsUnchanged`

**H2. Application — `GenerateFlashQuestCommandTests.cs`**
- `tests/LifeGrid.Application.Tests/FlashQuest/GenerateFlashQuestCommandTests.cs`
  - `AllGoalsAbove50Pct_NoOp_NoGeminiCall`
  - `LaggingGoalBelow50Pct_CallsGeminiAndInjectsFlashHabit`
  - `AiReturnsNotEligible_NoOp`
  - `AiReturnsUnknownSourceId_SkipsItem`
  - `AlreadyHasFlashHabits_SkipsPipelineEntirely`
  - `WeekNotFound_ReturnsFailure`

**H3. Application — `FlashQuestTriggerServiceTests.cs`**
- `tests/LifeGrid.Application.Tests/FlashQuest/FlashQuestTriggerServiceTests.cs`
  - `NotThursday_NoOp`
  - `Thursday_ActiveWeekExists_SendsGenerateFlashQuestCommand`
  - `Thursday_NoActiveWeek_NoOp`

**H4. Application — `LogHabitProgressCommandTests.cs` (extend)**
- `FlashBeforeDeadline_ActivatesDoubleXp`
- `FlashCompletion_ItselfIsDoubled`
- `FlashAfterDeadline_DoesNotActivateDoubleXp`
- `DoubleXpActive_StandardHabit20BaseXp_Awards40Xp` (the exact §5 TDD invariant from the requirements doc)
- Update existing 8 tests' assertions for the new `Result<LogHabitProgressResult>` return type.

**H5. Application — `GetHudTelemetryQueryTests.cs` (extend)**
- `IsDoubleXpActive_True_WhenProfileActive`
- `IsDoubleXpActive_False_WhenExpired`

**H6. Infrastructure**
- `tests/LifeGrid.Infrastructure.Tests/Repositories/HabitRepositoryTests.cs` (extend)
  - `HasFlashHabitsInWeek_Exists_ReturnsTrue`
  - `HasFlashHabitsInWeek_None_ReturnsFalse`
- `tests/LifeGrid.Infrastructure.Tests/AI/GeminiFlashQuestServiceTests.cs`
  - `ParsesAcceptedResponse_WithSourceHabitIds`
  - `ParsesNotEligible_OnLiteralNA`
  - `MalformedJson_ReturnsFailure`

---

## Estimated Test Count

| Layer | Before | New | After |
|---|---|---|---|
| Domain | 121 | 9 | 130 |
| Application | 188 | 16 | 204 |
| Infrastructure | 74 | 6 | 80 |
| **Total** | **383** | **31** | **414** |

*(Actuals: 204 Application tests, not 203 — one extra `Thursday_ClosedWeek_NoOp` case was added to `FlashQuestTriggerServiceTests` beyond the plan's three. 80 Infrastructure tests, not 79 — one extra `RateLimit_ReturnsFailureWithExceptionMessage` case was added to `GeminiFlashQuestServiceTests` beyond the plan's three, mirroring the existing `GeminiHabitGenerationServiceTests` convention.)*

---

## EF Migration

None required. `HabitType.Flash` and `UserActiveStates.DoubleXpMode`/`DoubleXpExpiry` are already-mapped columns from earlier phases, previously unused.

---

## Risk & Open Questions

| Risk | Mitigation |
|---|---|
| WorkManager `PeriodicWorkRequest` doesn't guarantee exact-minute firing | Same best-effort tolerance already accepted for the 09:00 Monday/Wednesday workers (Phase 30); Thursday 12:00 follows the identical pattern. |
| `LogHabitProgressCommand` return-type change is a breaking API change | Only one caller (`HabitLoggingViewModel`) and its test suite exist; both updated in this phase (C1/C2, G7, H4). |
| AI hallucinates a `source_habit_id` not in the request | Handler discards unmatched quests defensively (B1b step 9) rather than throwing. |
| `IDispatcherTimer` leak if a VM is recreated without disposing the previous timer | `HomeViewModel`/`WeeklyHabitsViewModel` are DI `Transient` per navigation (existing pattern); timer lifetime is scoped to the VM instance and stops being referenced on navigation. If leaks are observed in manual testing, revisit with explicit `OnDisappearing` teardown — out of scope to pre-empt without evidence. |
| Prompt8.txt schema change affects an already-authored asset | Both `docs/specs/assets/prompts/prompt8.txt` (reference copy) and the embedded Infrastructure copy are updated together (E2) so they never drift. |

---

## Implementation Notes (Post-Approval Corrections)

Full detail recorded in `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` § P32.11. Summary:

1. **`Application.Current` ambiguity** — `HomeViewModel.cs`/`WeeklyHabitsViewModel.cs` needed `Microsoft.Maui.Controls.Application.Current` fully qualified (CS0234) because both files already `using LifeGrid.Application.*`, which shadows the bare `Application` identifier.
2. **`WeekSummaryPage` Flash badge omits the countdown** — G4 was applied to all three pages, but only Home/WeeklyHabits own a live `IDispatcherTimer` (G2/G3); the read-only closed-week summary shows the Flash icon + label without a ticking `CountdownText`.
3. **Double XP tests use a local `UserProfile`, not the shared static `SeedProfile`** — avoids order-dependent flakiness in `LogHabitProgressCommandTests`, since `IsDoubleXpActive`/`WasDoubled` assertions are exact-state checks, unlike the pre-existing `LifetimeXp > 0` style assertions that tolerate a shared, accumulating profile.
4. **Test-fixture bug caught and fixed during the initial run** — `GenerateFlashQuestCommandTests` originally used a standalone `Guid habitId` instead of `habit.HabitId`, breaking the attribution dictionary and masking the happy-path test as a false negative (`QuestsInjected == 0`). Production code (`GenerateFlashQuestCommandHandler`) was correct throughout; only the test fixture needed correction.

All phases (A–H) implemented as planned with no scope changes. Full solution build: 0 errors. Full test suite: 414/414 passing.
