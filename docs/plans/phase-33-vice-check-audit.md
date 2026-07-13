# Phase 33 Plan: Vice Check Audit ("Test me I'm being good") & Penalties

**Status:** DONE — 459 tests passing (137 Domain / 229 Application / 93 Infrastructure)
**Requirements source:** `docs/requirements/Phase-33-requirements.md`
**Finalized requirements:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` (P33.1–P33.12)

---

## Clarifications Recorded (2026-07-10)

| # | Question | Answer |
|---|---|---|
| 1 | Stage 1 → Stage 2 context handoff & history | **Persisted `ViceCheckAudit` table** (standalone entity, like `Badge`/`Notification`). Stage 1 creates a `Pending` row and returns `(AuditId, Question)`; Stage 2 takes `(AuditId, Answer)` and resolves it in place. |
| 2 | Modal UI pattern | **In-page overlay** on `WeekSummaryPage.xaml`, matching the existing proof-image overlay convention. |
| 3 | Failure feedback styling | **Native `DisplayAlert` via `IToastNotificationService`** for both outcomes — add `ShowErrorAsync` alongside the existing `ShowInfoAsync`. |
| 4 | *(Added after initial approval)* Avoid repeating a previously-asked question for the same bad habit | **Yes.** `IViceCheckAuditRepository.GetPreviousQuestionsForBadHabitAsync(badHabitId)` feeds every prior `Question` asked about that `BadHabitId` (any `Status`) into a new `previous_questions` array on `Prompt6.txt`'s payload; `Prompt6.txt` gained a new rule 3 ("Avoid Repetition") instructing the AI to differ meaningfully from every listed prior question. |

## Ambiguities Resolved Without Asking (see P33.2 for full reasoning)

| # | Ambiguity | Resolution |
|---|---|---|
| 1 | AI vs. our-code random selection | **Our code selects** (Phase-33-requirements.md §2.2 orders "Context Selection" *before* "AI Generation"); `Prompt6.txt` receives a single-element payload, sidestepping any need to extend its schema with IDs. |
| 2 | `Danger_Level` range conflict (1-5 vs 1-10 across two source docs) | No code impact — `DangerLevel` is an unconstrained `int` already; formula and code work identically either way. |
| 3 | Modal cancel/dismiss | **None offered.** XP is already irreversibly granted at Stage 1; no cancel button, keeping the once-per-week guard trivial. |
| 4 | "Local device time" | **`IDateTimeProvider.UtcNow`**, matching every other temporal check in the app. |

---

## Pre-Flight Architecture Notes

- `LinkedBadHabit` is already an EF owned entity on `Goal` (table `GoalLinkedBadHabits`, keyed by `BadHabitId`) — no changes needed there; `ViceCheckAudit` references its `BadHabitId` as a loose `Guid`, not an EF-level FK (consistent with `Notification.DeepLinkUrl`-style loose references elsewhere).
- `ProcrastinationEscalationEngine.Evaluate` (Phase 31) and `UseShieldCommand`/`CloseWeekCommandHandler`'s pattern of loading a `WeekGoal`, mutating it, and calling `SetPenaltyState`/`ApplyXpPenalty` are reused as-is — no engine changes.
- `Week.EndDate` is never persisted (`StartDate.AddDays(6)`, Phase 30 convention) — the new `ViceCheckAvailabilityComputer` reuses this exact calculation.
- `IWeekRepository.GetByIdAsync` already includes `WeekGoals` via EF `Include` (established in Phase 31) — no repository change needed for that load.
- `WeeklyHabitsDashboardDto` is shared by both `GetWeeklyHabitsQueryHandler` (WeekSummary) and `GetCurrentWeekHabitsQueryHandler` (Home) — per the Phase 31 precedent, both handlers must be touched even though only the WeekSummary path can ever produce `IsViceCheckAvailable == true`.
- This is the first phase to add a **new EF migration** since Phase 29 (Notifications) — most recent phases (30–32) reused existing schema.

---

## Implementation Phases

---

### Phase A: Domain Layer

**A1. New `ViceCheckAudit` aggregate**
- Files: `src/LifeGrid.Domain/ViceCheck/ViceCheckStatus.cs`, `src/LifeGrid.Domain/ViceCheck/ViceCheckAudit.cs`
- `ViceCheckStatus`: `Pending`, `Passed`, `Failed`.
- `ViceCheckAudit`:
  ```csharp
  public static ViceCheckAudit Create(
      Guid weekId, Guid weekGoalId, Guid badHabitId,
      string goalDescription, string badHabitDescription, int dangerLevel,
      string question, DateTime createdAt) => new()
  {
      AuditId = Guid.NewGuid(), WeekId = weekId, WeekGoalId = weekGoalId, BadHabitId = badHabitId,
      GoalDescription = goalDescription, BadHabitDescription = badHabitDescription, DangerLevel = dangerLevel,
      Question = question, Status = ViceCheckStatus.Pending, CreatedAt = createdAt
  };

  public void MarkPassed(string answer, DateTime resolvedAt)
  {
      Answer = answer; Status = ViceCheckStatus.Passed; ResolvedAt = resolvedAt;
  }

  public void MarkFailed(string answer, double penaltyPercent, DateTime resolvedAt)
  {
      Answer = answer; Status = ViceCheckStatus.Failed;
      PenaltyPercentApplied = penaltyPercent; ResolvedAt = resolvedAt;
  }
  ```
  All properties `private set`; guard via `Status == Pending` enforced at the Application layer (handler), not the entity, consistent with existing entities in this codebase (e.g. `WeekGoal` doesn't self-guard `SetPenaltyState`).

**A2. `WeekGoal` — retroactive penalty method**
- File: `src/LifeGrid.Domain/WeekGoal/WeekGoal.cs`
- Add:
  ```csharp
  public void ApplyRetroactiveGpPenalty(double newGp) => GoalWeeklyGp = Math.Max(0.0, newGp);
  ```

**A3. `GamificationCalculationEngine` — penalty math**
- File: `src/LifeGrid.Domain/Gamification/GamificationCalculationEngine.cs`
- Add:
  ```csharp
  public static double ApplyVicePenalty(double currentGp, int dangerLevel)
      => Math.Max(0.0, currentGp - dangerLevel);
  ```

---

### Phase B: Application Layer — Availability & Shared Types

**B1. `ViceCheckAvailabilityComputer`**
- File: `src/LifeGrid.Application/ViceCheck/ViceCheckAvailabilityComputer.cs`
```csharp
public static class ViceCheckAvailabilityComputer
{
    public static bool IsVisible(
        bool isViceSurveyCompleted, bool isWeekClosed, DateTime weekStartDate,
        DateTime now, bool alreadyAudited)
    {
        if (!isViceSurveyCompleted || !isWeekClosed || alreadyAudited) return false;
        var cutoff = weekStartDate.AddDays(6).AddHours(72);
        return now <= cutoff;
    }
}
```

**B2. `IViceCheckAuditRepository`**
- File: `src/LifeGrid.Application/ViceCheck/IViceCheckAuditRepository.cs`
```csharp
public interface IViceCheckAuditRepository
{
    Task AddAsync(ViceCheckAudit audit, CancellationToken ct = default);
    Task<ViceCheckAudit?> GetByIdAsync(Guid auditId, CancellationToken ct = default);
    Task<bool> HasAuditForWeekAsync(Guid weekId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetPreviousQuestionsForBadHabitAsync(Guid badHabitId, CancellationToken ct = default);
}
```
`GetPreviousQuestionsForBadHabitAsync` powers the repeat-avoidance rule (Clarification #4) — returns every `Question` ever recorded against `badHabitId`, regardless of `Status`.

**B3. `IRandomProvider`**
- File: `src/LifeGrid.Application/Common/IRandomProvider.cs`
```csharp
public interface IRandomProvider { int Next(int maxExclusive); }
```

**B4. `IGeminiViceCheckService`**
- File: `src/LifeGrid.Application/ViceCheck/IGeminiViceCheckService.cs`
```csharp
public interface IGeminiViceCheckService
{
    Task<Result<GenerateQuestionResult>> GenerateQuestionAsync(string goalAndHabitJson, CancellationToken ct = default);
    Task<Result<EvaluateAnswerResult>> EvaluateAnswerAsync(string userResponseJson, CancellationToken ct = default);
}
public record GenerateQuestionResult(string Question);
public record EvaluateAnswerResult(bool Persists);
```

---

### Phase C: Application Layer — `InitiateViceCheckCommand` (Stage 1)

**C1. Command + Handler**
- File: `src/LifeGrid.Application/ViceCheck/InitiateViceCheckCommand.cs`
```csharp
public record InitiateViceCheckCommand(Guid WeekId) : IRequest<Result<InitiateViceCheckResult>>;
public record InitiateViceCheckResult(Guid AuditId, string Question);
```
- Handler dependencies: `IWeekRepository`, `IGoalRepository`, `IUserProfileRepository`, `IViceCheckAuditRepository`, `IGeminiViceCheckService`, `IRandomProvider`, `IDateTimeProvider`, `IUnitOfWork`, `IEconomyStateBroadcaster`.
- Logic (full detail in FUNCTIONAL_REQUIREMENTS.md P33.5):
  1. Load week (incl. `WeekGoals`) → `Failure("week_not_found")`.
  2. Guard `week.Status == Closed` → `Failure("week_not_closed")`.
  3. Load profile; guard `IsViceSurveyCompleted` → `Failure("vice_survey_not_completed")`.
  4. Guard temporal window (`ViceCheckAvailabilityComputer`-equivalent check) and `!HasAuditForWeekAsync` → `Failure("window_expired")` / `Failure("already_audited")`.
  5. Load all goals for `week.WeekGoals`; flatten `(WeekGoal, Goal, LinkedBadHabit)` triples; `Failure("no_bad_habits_available")` if empty.
  6. `randomProvider.Next(triples.Count)` → select one triple.
  7. `previousQuestions = await auditRepo.GetPreviousQuestionsForBadHabitAsync(selected.BadHabit.BadHabitId, ct)`.
  8. Serialize as single-element `[{description, bad_habits:[{description, danger_level, previous_questions}]}]` payload, with `previous_questions` set to `previousQuestions` (empty array if none).
  9. `geminiViceCheckService.GenerateQuestionAsync(...)` — propagate failure.
  10. `profile.ApplyXpAndLevelProgression(20)` — Lifetime XP only.
  11. `ViceCheckAudit.Create(...)` with `Status = Pending` → `auditRepo.AddAsync`.
  12. `unitOfWork.CommitAsync()` (single transaction: XP + audit row).
  13. `broadcaster.BroadcastEconomy(...)`.
  14. Return `Success(new(audit.AuditId, audit.Question))`.

---

### Phase D: Application Layer — `ResolveViceCheckCommand` (Stage 2)

**D1. Command + Handler**
- File: `src/LifeGrid.Application/ViceCheck/ResolveViceCheckCommand.cs`
```csharp
public record ResolveViceCheckCommand(Guid AuditId, string Answer) : IRequest<Result<ResolveViceCheckResult>>;
public record ResolveViceCheckResult(bool Persists, double? NewGp, double? PenaltyPercent, bool TriggersOverwhelmed);
```
- Handler dependencies: `IViceCheckAuditRepository`, `IWeekRepository`, `IGoalRepository`, `IGeminiViceCheckService`, `IDateTimeProvider`, `IUnitOfWork`, `IEconomyStateBroadcaster`.
- Logic (full detail in FUNCTIONAL_REQUIREMENTS.md P33.6):
  1. Load audit → `Failure("audit_not_found")`; guard `Status == Pending` → `Failure("already_resolved")`.
  2. Build Prompt7 payload from audit's cached context + `request.Answer`.
  3. `geminiViceCheckService.EvaluateAnswerAsync(...)` — propagate failure.
  4. If `!persists`: `audit.MarkPassed(...)` → commit → `Success(new(false, null, null, false))`.
  5. If `persists`:
     a. Load `WeekGoal` by `audit.WeekGoalId` → `Failure("weekgoal_not_found")`.
     b. `newGp = GamificationCalculationEngine.ApplyVicePenalty(weekGoal.GoalWeeklyGp, audit.DangerLevel)`.
     c. `weekGoal.ApplyRetroactiveGpPenalty(newGp)`.
     d. `esc = ProcrastinationEscalationEngine.Evaluate(weekGoal.PenaltyState, newGp, weekGoal.GoalWeeklyXpEarned)`.
     e. `weekGoal.SetPenaltyState(esc.NewPenaltyState)`; `weekGoal.ApplyXpPenalty(esc.PenalizedXp)`.
     f. If `esc.TriggersOverwhelmed`: load `Goal` → `MarkOverwhelmed()`.
     g. `audit.MarkFailed(request.Answer, audit.DangerLevel, now)`.
     h. `unitOfWork.CommitAsync()` (single transaction).
     i. `broadcaster.Broadcast()`.
     j. Return `Success(new(true, newGp, audit.DangerLevel, esc.TriggersOverwhelmed))`.

---

### Phase E: Application Layer — Dashboard Wiring

**E1. `WeeklyHabitsDashboardDto` — add field**
- File: `src/LifeGrid.Application/WeeklyHabits/WeeklyHabitsDashboardDto.cs`
- Add `bool IsViceCheckAvailable` to the record.

**E2. `GetWeeklyHabitsQueryHandler` — populate field**
- File: `src/LifeGrid.Application/WeeklyHabits/GetWeeklyHabitsQueryHandler.cs`
- Add `IViceCheckAuditRepository auditRepository` and `IDateTimeProvider dateTimeProvider` to the constructor.
- Compute: `var isViceCheckAvailable = ViceCheckAvailabilityComputer.IsVisible(profile?.IsViceSurveyCompleted ?? false, week.Status == WeekStatus.Closed, week.StartDate, dateTimeProvider.UtcNow, await auditRepository.HasAuditForWeekAsync(week.WeekId, cancellationToken));`
- Pass into the `WeeklyHabitsDashboardDto` constructor call.

**E3. `GetCurrentWeekHabitsQueryHandler` — same wiring**
- File: `src/LifeGrid.Application/Home/GetCurrentWeekHabitsQueryHandler.cs`
- Same constructor/field additions as E2 (per Phase 31 precedent — both handlers build the same DTO type). Will always resolve to `false` in practice since the current week is never `Closed`, but keeps the DTO consistent.

---

### Phase F: Infrastructure Layer

**F1. `ViceCheckAuditConfiguration`**
- File: `src/LifeGrid.Infrastructure/Data/EntityConfigurations/ViceCheckAuditConfiguration.cs`
- New table `ViceCheckAudits`; `AuditId` PK (`ValueGeneratedNever`); `Status` via `.HasConversion<string>()`; `WeekId`/`WeekGoalId`/`BadHabitId` as plain `Guid` columns (no FK constraints — loose references, consistent with `Notification`).

**F2. `LifeGridDbContext` — add `DbSet`**
- File: `src/LifeGrid.Infrastructure/Data/LifeGridDbContext.cs`
- Add `public DbSet<ViceCheckAudit> ViceCheckAudits => Set<ViceCheckAudit>();`

**F3. EF Migration**
- Run `dotnet ef migrations add Phase33_AddViceCheckAudits` against `LifeGrid.Infrastructure` — adds only the `ViceCheckAudits` table.

**F4. `ViceCheckAuditRepository`**
- File: `src/LifeGrid.Infrastructure/Data/Repositories/ViceCheckAuditRepository.cs`
- Standard EF implementation of `IViceCheckAuditRepository`:
  ```csharp
  public Task AddAsync(ViceCheckAudit audit, CancellationToken ct = default)
  { db.ViceCheckAudits.Add(audit); return Task.CompletedTask; }

  public Task<ViceCheckAudit?> GetByIdAsync(Guid auditId, CancellationToken ct = default)
      => db.ViceCheckAudits.FirstOrDefaultAsync(a => a.AuditId == auditId, ct);

  public Task<bool> HasAuditForWeekAsync(Guid weekId, CancellationToken ct = default)
      => db.ViceCheckAudits.AnyAsync(a => a.WeekId == weekId, ct);

  public async Task<IReadOnlyList<string>> GetPreviousQuestionsForBadHabitAsync(
      Guid badHabitId, CancellationToken ct = default)
      => await db.ViceCheckAudits
          .Where(a => a.BadHabitId == badHabitId)
          .Select(a => a.Question)
          .ToListAsync(ct);
  ```

**F5. `SystemRandomProvider`**
- File: `src/LifeGrid.Infrastructure/Common/SystemRandomProvider.cs`
```csharp
internal sealed class SystemRandomProvider : IRandomProvider
{
    public int Next(int maxExclusive) => Random.Shared.Next(maxExclusive);
}
```

**F6. `prompt6.txt` / `prompt7.txt` — embed**
- `docs/specs/assets/prompts/prompt6.txt` has already been updated (pre-implementation) with a new rule 3 ("Avoid Repetition") and a `previous_questions` field on each bad-habit entry — copy that **updated** version verbatim into `src/LifeGrid.Infrastructure/AI/Prompts/prompt6.txt`. Copy `prompt7.txt` verbatim (no schema change).
- Add both as `<EmbeddedResource Include="AI\Prompts\prompt6.txt" />` / `prompt7.txt` in `LifeGrid.Infrastructure.csproj`.
- As with Phase 32's `prompt8.txt`, keep the docs reference copy and the embedded Infrastructure copy in sync — any future prompt edits must land in both.

**F7. `GeminiViceCheckService`**
- File: `src/LifeGrid.Infrastructure/AI/GeminiViceCheckService.cs`
- Follows the exact `GeminiViceSurveyService` pattern (embedded prompt templates, placeholder substitution, `StripCodeFences`, try/catch → `Result.Failure`).
- `GenerateQuestionAsync`: substitutes `${GOALS_AND_HABITS_JSON}` in `prompt6.txt` with the caller-supplied single-element payload (which already embeds `previous_questions` per bad habit — the service does not construct this itself, it only substitutes the placeholder); parses response, extracts `ambient_question` only (other echoed fields discarded — caller already has the canonical values).
- `EvaluateAnswerAsync`: substitutes `${USER_RESPONSE_JSON}` in `prompt7.txt`; parses response, extracts `persists` only (`analysis_reasoning` discarded).

**F8. DI registration**
- File: `src/LifeGrid.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs`
- Add:
  ```csharp
  services.AddScoped<IViceCheckAuditRepository, ViceCheckAuditRepository>();
  services.AddSingleton<IRandomProvider, SystemRandomProvider>();
  services.AddTransient<IGeminiViceCheckService, GeminiViceCheckService>();
  ```

---

### Phase G: Presentation Layer

**G1. `IToastNotificationService` — add error variant**
- File: `src/LifeGrid.Application/Common/IToastNotificationService.cs`
- Add: `Task ShowErrorAsync(string title, string message, CancellationToken ct = default);`
- File: `src/LifeGrid.Presentation/Services/MauiToastNotificationService.cs`
- Implement identically to `ShowInfoAsync` (native `DisplayAlertAsync`) — same native-alert mechanism, distinguished only by title/message content passed by the caller.

**G2. `WeekSummaryViewModel` — Vice Check state & commands**
- File: `src/LifeGrid.Presentation/ViewModels/WeekSummaryViewModel.cs`
- Inject `IToastNotificationService toastService` via constructor (alongside existing `IMediator`).
- Add:
  ```csharp
  [ObservableProperty] private bool   _isViceCheckAvailable;
  [ObservableProperty] private bool   _isViceCheckOverlayVisible;
  [ObservableProperty] private string _viceCheckQuestion = string.Empty;
  [ObservableProperty] private string _viceCheckAnswer   = string.Empty;
  [ObservableProperty] private bool   _isViceCheckBusy;
  private Guid? _currentAuditId;
  ```
- `LoadAsync()`: set `IsViceCheckAvailable = dto.IsViceCheckAvailable;`
- `[RelayCommand] InitiateViceCheckAsync()`:
  ```csharp
  var result = await _mediator.Send(new InitiateViceCheckCommand(_weekId));
  if (!result.IsSuccess)
  {
      await toastService.ShowErrorAsync("Unavailable", result.Error ?? "Could not start the check.");
      return;
  }
  _currentAuditId = result.Value!.AuditId;
  ViceCheckQuestion = result.Value.Question;
  ViceCheckAnswer   = string.Empty;
  IsViceCheckOverlayVisible = true;
  ```
- `[RelayCommand] SubmitViceCheckAnswerAsync()`:
  ```csharp
  if (string.IsNullOrWhiteSpace(ViceCheckAnswer) || _currentAuditId is null) return;
  IsViceCheckBusy = true;
  var result = await _mediator.Send(new ResolveViceCheckCommand(_currentAuditId.Value, ViceCheckAnswer));
  IsViceCheckBusy = false;
  IsViceCheckOverlayVisible = false;

  if (!result.IsSuccess)
  {
      await toastService.ShowErrorAsync("Error", result.Error ?? "Could not resolve the check.");
      return;
  }

  if (result.Value!.Persists)
      await toastService.ShowErrorAsync("Vice Detected",
          $"Vice detected. -{result.Value.PenaltyPercent:F0}% GP applied retroactively.");
  else
      await toastService.ShowInfoAsync("Integrity Maintained", "Integrity maintained. 20 XP secured.");

  await LoadAsync(); // refreshes GP display and re-fetches IsViceCheckAvailable (now false)
  ```
- No dismiss/cancel command (P33.2 note 3).

**G3. `WeekSummaryPage.xaml` — button + overlay**
- File: `src/LifeGrid.Presentation/Pages/WeekSummaryPage.xaml`
- Add "Test me I'm being good" `Button`, `IsVisible="{Binding IsViceCheckAvailable}"`, `Command="{Binding InitiateViceCheckCommand}"`, styled as a secondary action (per style guide — likely `Secondary`/`OnSecondary` tokens, `CornerRadius="2"`).
- Add a full-page overlay `Grid` (semi-transparent `BackgroundColor`, `IsVisible="{Binding IsViceCheckOverlayVisible}"`) containing:
  - `Label` bound to `ViceCheckQuestion`, primary typography token (per spec §3.2).
  - `Editor` (multi-line), `Text="{Binding ViceCheckAnswer}"`.
  - "Submit" `Button`, `Command="{Binding SubmitViceCheckAnswerCommand}"`, `IsEnabled="{Binding IsViceCheckBusy, Converter={StaticResource InverseBool}}"`.
  - `ActivityIndicator`, `IsRunning="{Binding IsViceCheckBusy}"`, `IsVisible="{Binding IsViceCheckBusy}"`.

**G4. `MauiProgram.cs`**
- No new DI registrations needed here — `WeekSummaryViewModel`/`WeekSummaryPage` are already registered (Phase 30); only the constructor signature changes (still resolved automatically by the DI container).

---

### Phase H: Tests

**H1. Domain**
- `tests/LifeGrid.Domain.Tests/ViceCheck/ViceCheckAuditTests.cs`
  - `Create_SetsStatusToPending`
  - `MarkPassed_SetsStatusAndAnswer`
  - `MarkFailed_SetsStatusAnswerAndPenaltyPercent`
- `tests/LifeGrid.Domain.Tests/WeekGoal/WeekGoalRetroactivePenaltyTests.cs`
  - `ApplyRetroactiveGpPenalty_SetsNewGp`
  - `ApplyRetroactiveGpPenalty_ClampsAtZero`
- `tests/LifeGrid.Domain.Tests/Gamification/GamificationCalculationEngineTests.cs` (extend)
  - `ApplyVicePenalty_DangerLevel5_Gp82_ReturnsGp77` (§4 Penalty Math Test)
  - `ApplyVicePenalty_ClampsAtZero`

**H2. Application — `ViceCheckAvailabilityComputerTests.cs`**
- File: `tests/LifeGrid.Application.Tests/ViceCheck/ViceCheckAvailabilityComputerTests.cs`
  - `WithinWindow_ReturnsTrue`
  - `AtExactCutoff_ReturnsTrue`
  - `73HoursPastEndDate_ReturnsFalse` (§4 Temporal Visibility Test, using `EndDate = StartDate.AddDays(6)`)
  - `SurveyNotCompleted_ReturnsFalse`
  - `WeekNotClosed_ReturnsFalse`
  - `AlreadyAudited_ReturnsFalse`

**H3. Application — `InitiateViceCheckCommandTests.cs`**
- File: `tests/LifeGrid.Application.Tests/ViceCheck/InitiateViceCheckCommandTests.cs`
  - `HappyPath_AwardsXpAndCreatesAudit`
  - `HappyPath_AwardsXp_IndependentOfLaterOutcome` (§4 Instant XP Test — asserts XP granted at commit, no dependency on Stage 2)
  - `WeekNotFound_ReturnsFailure`
  - `WeekNotClosed_ReturnsFailure`
  - `SurveyNotCompleted_ReturnsFailure`
  - `AlreadyAudited_ReturnsFailure`
  - `WindowExpired_ReturnsFailure`
  - `NoBadHabitsAvailable_ReturnsFailure`
  - `RandomProvider_UsedToSelectAmongMultipleTriples` (mocked `IRandomProvider` returning a fixed index → assert the *correct* triple's `WeekGoalId`/`BadHabitId` land on the created audit)
  - `IncludesPreviousQuestionsForSelectedBadHabit_InGeminiPayload` (mocked `auditRepo.GetPreviousQuestionsForBadHabitAsync` returns 2 prior questions for the selected `BadHabitId` → captured payload sent to `IGeminiViceCheckService.GenerateQuestionAsync` contains a `previous_questions` array with both)
  - `NoPriorAudits_SendsEmptyPreviousQuestionsArray` (mocked repo returns empty list → payload's `previous_questions` is `[]`, not omitted)

**H4. Application — `ResolveViceCheckCommandTests.cs`**
- File: `tests/LifeGrid.Application.Tests/ViceCheck/ResolveViceCheckCommandTests.cs`
  - `PersistsFalse_NoGpChange_MarksPassed`
  - `PersistsTrue_DangerLevel5_Gp82_AppliesPenalty_ShiftsCleanToLevel1Warning` (full end-to-end Penalty Math Test)
  - `PersistsTrue_CascadesToOverwhelmed_WhenAlreadyProbation` (confirms full engine reuse, not just the boundary case)
  - `AuditNotFound_ReturnsFailure`
  - `AlreadyResolved_ReturnsFailure`
  - `AiFailure_PropagatesFailure`

**H5. Application — `GetWeeklyHabitsQueryTests.cs` / `GetCurrentWeekHabitsQueryTests.cs` (extend)**
- `IsViceCheckAvailable_True_WhenAllGatesPass`
- `IsViceCheckAvailable_False_WhenAlreadyAudited`

**H6. Infrastructure**
- `tests/LifeGrid.Infrastructure.Tests/Data/ViceCheckAuditRepositoryTests.cs`
  - `AddAsync_PersistsAfterCommit`
  - `HasAuditForWeek_Exists_ReturnsTrue` / `_None_ReturnsFalse`
  - `GetById_ReturnsCorrectAudit` / `_Unknown_ReturnsNull`
  - `GetPreviousQuestionsForBadHabit_ReturnsAllPriorQuestions_AnyStatus` (seed one `Passed` and one `Pending` audit for the same `BadHabitId` → both `Question`s returned)
  - `GetPreviousQuestionsForBadHabit_NoPriorAudits_ReturnsEmpty`
  - `GetPreviousQuestionsForBadHabit_ScopedToBadHabitId_ExcludesOtherHabits` (a prior audit for a *different* `BadHabitId` is not returned)
- `tests/LifeGrid.Infrastructure.Tests/AI/GeminiViceCheckServiceTests.cs`
  - `GenerateQuestion_ExtractsAmbientQuestion`
  - `GenerateQuestion_MalformedJson_ReturnsFailure`
  - `EvaluateAnswer_ExtractsPersistsTrue` / `_ExtractsPersistsFalse`
  - `EvaluateAnswer_MalformedJson_ReturnsFailure`

---

## Estimated Test Count

| Layer | Before | New | After |
|---|---|---|---|
| Domain | 130 | 7 | 137 |
| Application | 204 | 24 | 228 |
| Infrastructure | 80 | 12 | 92 |
| **Total** | **414** | **43** | **457** |

*(Actuals: 137 Domain — exact match. 229 Application, not 228 — one extra edge-case test. 93 Infrastructure, not 92 — one extra edge-case test (`GetById_Unknown_ReturnsNull`). Total 459.)*

---

## EF Migration

**Required** — `Phase33_AddViceCheckAudits`, adding the standalone `ViceCheckAudits` table (see Phase F). No changes to any existing table/column.

---

## Risk & Open Questions

| Risk | Mitigation |
|---|---|
| A `Pending` audit permanently blocks the button if the app is killed mid-modal (no resume flow) | Accepted for this phase's scope (P33.2 note 3) — the user has already banked the +20 XP, and this is a narrow edge case (mid-modal app kill) rather than a routine path. Flag for a future phase if it proves to be a real user complaint. |
| `WeeklyHabitsDashboardDto` constructor change (2 new params: `IViceCheckAuditRepository`, `IDateTimeProvider`) touches two handlers | Both `GetWeeklyHabitsQueryHandler` and `GetCurrentWeekHabitsQueryHandler` updated together (E2/E3), mirroring the exact precedent set by Phase 31's `ShieldsAvailable` addition. |
| Retroactively penalizing an already-closed `WeekGoal` outside the normal `CloseWeekCommand` flow could double-penalize XP if `ApplyXpPenalty` fires again on a subsequent, unrelated mutation | `ApplyXpPenalty` sets `GoalWeeklyXpEarned` absolutely (not additively) via `ProcrastinationEscalationEngine`'s returned value each time it's invoked — same non-additive semantics `CloseWeekCommandHandler` already relies on, so no double-penalty risk. |
| New EF migration risk (first schema change since Phase 29) | Migration adds a single new table with no foreign keys to existing tables — minimal blast radius, no data migration needed for existing rows. |

---

## Implementation Notes (Post-Approval Corrections)

Full detail recorded in `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` § P33.12. Summary:

1. **EF migration generation needed `--project` only, no `--startup-project`** — `LifeGrid.Presentation` is single-targeted at `net10.0-android` via a plural `<TargetFrameworks>` tag, which makes `dotnet ef` treat it as multi-targeted and fail (`MSB4057`), and `--framework net10.0-android` fails too since `LifeGrid.Infrastructure` doesn't build for Android. Fix: `LifeGrid.Infrastructure` already has a `LifeGridDbContextFactory : IDesignTimeDbContextFactory<LifeGridDbContext>` from an earlier phase — running the command against `LifeGrid.Infrastructure` alone (no startup project) lets EF Core discover it directly.
2. **`InitiateViceCheckCommandHandler` inlines the final temporal check** rather than re-calling `ViceCheckAvailabilityComputer.IsVisible(...)` with already-validated true values for the other gates — same behavior, clearer code. `ViceCheckAvailabilityComputer` itself is unchanged and remains the single source of truth for the read-only dashboard flag.
3. **Test-authoring bug caught pre-run** — two new tests initially `await`ed an NSubstitute `Arg.Do` configuration call as if it were the real invocation; fixed before the first test run (not a runtime failure). All three suites passed on their first actual execution.

All phases (A–H) implemented as planned with no scope changes. Full solution build: 0 errors. Full test suite: 459/459 passing.
