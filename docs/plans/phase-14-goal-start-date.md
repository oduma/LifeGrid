# Phase 14 — Goal Start Date: User-Chosen Scheduling Anchor
**Status:** DONE — completed 2026-06-22

## Source Documents
- Requirements: `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` §P14
- Clarifications answered: 2026-06-22
- Prompt specs: `docs/specs/assets/prompts/Prompt1.txt`, `prompt2.1.txt`, `prompt2.2.txt`

---

## Clarification Answers
| # | Question | Answer |
|---|---|---|
| Q1 | Date picker placement | Same screen as goal text entry (State A), before "Next" button |
| Q2 | Start date replaces DateTime.UtcNow in Prompt1 anchor? | Yes — start date is the base for all deadline calculations |
| Q3 | Goal.Create() accepts startDate directly? | Yes; creationDate kept as audit timestamp |
| Q4 | OnboardingSession caches chosen start date? | Yes — new ChosenStartDate field + EF migration |

---

## Architecture Overview

```
[SetupPage — State A]
  DatePicker (Mondays only, min = current week Monday)
    → CreateGoalViewModel.ChosenStartDate

[CompleteStep1Command click]
  → TriggerGoalValidationCommand(ChosenStartDate)
      → session.SetChosenStartDate(chosenStartDate)
      → ValidateGoalAsync(draft, startDate)     ← "current date" = startDate
          Prompt1: ${START_DATE} = startDate
          deadline = startDate + user's stated duration

[FinalizeGoalCommand]
  → Goal.Create(userId, ..., startDate = session.ChosenStartDate, creationDate = DateTime.Now)
  → goal.StartDate = user's chosen Monday (persisted to Goals table)

[GenerateHabitsCommand]
  → GenerateScheduleAsync(desc, deadline, baseline, startDate = goal.StartDate)
      Prompt2.1: ${START_DATE} = goal.StartDate
          → start_date included in output JSON
      Prompt2.2: reads start_date from Prompt2.1's output
          → Week 1 start_date = user's chosen Monday
  → GenerateHabitsCommand deduplication (unchanged):
      GetByStartDateAsync → reuse existing Week if found; else create new
```

---

## Phase 1 — Tests (TDD: write first)

### 1.1 Update `tests/LifeGrid.Domain.Tests/Goal/GoalStartDateTests.cs`

**Remove** three tests that verify auto-calculation via `Create()` (these tests describe the old behaviour):
- `Create_SetsStartDateToComputedMonday`
- `Create_WhenCreatedOnMonday_StartDateEqualsCreationDate`
- `Create_StartDateDayOfWeek_IsAlwaysMonday`

**Add** two replacement tests:
```
Create_WithExplicitStartDate_StoresItDirectly
  — Goal.Create(..., startDate: new DateTime(2026,6,22), creationDate: any)
  → goal.StartDate should be 2026-06-22

Create_StoresCreationDate
  — Goal.Create(..., startDate: any Monday, creationDate: new DateTime(2026,6,18))
  → goal.CreationDate should be 2026-06-18
```

The seven `CalculateStartDate` formula tests and `CalculateStartDate_ResultIsAlwaysMidnight` remain unchanged — the static method is kept.

### 1.2 Update `tests/LifeGrid.Domain.Tests/Goal/GoalTests.cs`
Update `BuildGoal()` helper: replace `creationDate: DateTime.Now` with both
`startDate: new DateTime(2026, 6, 22)` (a known Monday) and `creationDate: DateTime.Now`.

### 1.3 Update all tests calling `Goal.Create()` with old signature
Files:
- `tests/LifeGrid.Domain.Tests/Goal/GoalDomainMutationTests.cs`
- `tests/LifeGrid.Application.Tests/Goal/AbandonGoalCommandHandlerTests.cs`
- `tests/LifeGrid.Application.Tests/Goal/RecalculateGoalScheduleCommandHandlerTests.cs`
- `tests/LifeGrid.Application.Tests/Week/GetTimelineQueryHandlerTests.cs` — `MakeGoal()` helper
- `tests/LifeGrid.Application.Tests/Goal/GetGoalsQueryHandlerTests.cs`

For each: add `startDate: new DateTime(2026, 6, 22)` (canonical test Monday) and `creationDate: DateTime.Now` (or any existing datetime arg).

### 1.4 Update `tests/LifeGrid.Application.Tests/Onboarding/TriggerGoalValidationCommandHandlerTests.cs`

**Update all 7 existing tests:**
- Replace `new TriggerGoalValidationCommand()` → `new TriggerGoalValidationCommand(new DateTime(2026, 6, 22))`
- Update `_gemini.ValidateGoalAsync(Arg.Any<string>(), ...)` → `_gemini.ValidateGoalAsync(Arg.Any<string>(), Arg.Any<DateTime>(), ...)`

**Add 2 new tests:**
```
ChosenStartDate_IsStoredInSession
  — Run happy-path flow with chosenStartDate = 2026-06-22
  → session.ChosenStartDate should be 2026-06-22 after handler runs

ChosenStartDate_IsPassedToGemini
  — Verify _gemini.ValidateGoalAsync was called with startDate == 2026-06-22
```

### 1.5 Update `tests/LifeGrid.Application.Tests/Onboarding/FinalizeGoalCommandHandlerTests.cs`
Add `session.SetChosenStartDate(new DateTime(2026, 6, 22))` to all test setups that provide a session.

**Add 1 new test:**
```
UsesSessionChosenStartDate_ForGoalStartDate
  — Set session.ChosenStartDate = 2026-06-22
  → goalRepository.AddAsync should be called with goal where goal.StartDate == 2026-06-22
```

### 1.6 Update `tests/LifeGrid.Application.Tests/Habit/GenerateHabitsCommandTests.cs`
Update all `_aiService.GenerateScheduleAsync(...)` mock setups to include the `DateTime startDate` parameter:
```csharp
_aiService.GenerateScheduleAsync(
    Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
    Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
    .Returns(...);
```

**Add 1 new test:**
```
GoalStartDate_IsPassedToHabitGenerationService
  — goal.StartDate = 2026-06-22
  → _aiService.GenerateScheduleAsync received call with startDate == 2026-06-22
```

### 1.7 Update `tests/LifeGrid.Infrastructure.Tests/AI/GeminiGoalValidationServiceTests.cs`
Update every call to `service.ValidateGoalAsync(...)` to include a `startDate` argument.

### 1.8 Update `tests/LifeGrid.Infrastructure.Tests/AI/GeminiHabitGenerationServiceTests.cs`
Update every call to `service.GenerateScheduleAsync(...)` to include a `startDate` argument.

---

## Phase 2 — Domain Layer

### 2.1 `src/LifeGrid.Domain/Goal/Goal.cs`

**Add property:**
```csharp
public DateTime CreationDate { get; private set; }
```

**Change `Create()` signature** (add `startDate`; rename existing param):
```csharp
public static Goal Create(
    Guid     userId,
    string   description,
    string   ambientTag,
    string   duration,
    DateTime deadlineDate,
    DateTime startDate,        // NEW — user-chosen Monday
    DateTime creationDate)     // KEPT — audit timestamp
=> new()
{
    GoalId       = Guid.NewGuid(),
    UserId       = userId,
    Description  = description,
    AmbientTag   = ambientTag,
    Duration     = duration,
    DeadlineDate = deadlineDate,
    StartDate    = startDate,         // direct assignment, no calculation
    CreationDate = creationDate,      // NEW
    Status       = GoalStatus.Active
};
```

**Keep `CalculateStartDate(DateTime date)` static method unchanged** — still used by ViewModel, RecalculateCommand, and snap logic.

### 2.2 `src/LifeGrid.Domain/Onboarding/OnboardingSession.cs`

**Add property:**
```csharp
public DateTime? ChosenStartDate { get; private set; }
```

**Add method:**
```csharp
public void SetChosenStartDate(DateTime date)
{
    ChosenStartDate     = date;
    LastActiveTimestamp = DateTime.UtcNow;
}
```

**Update `Reset()`** — add `ChosenStartDate = null;`

---

## Phase 3 — Application Layer: Interface Updates

### 3.1 `src/LifeGrid.Application/Goal/IGeminiGoalValidationService.cs`
```csharp
Task<Result<GeminiValidationResult>> ValidateGoalAsync(
    string rawDraft, DateTime startDate, CancellationToken ct = default);
```

### 3.2 `src/LifeGrid.Application/Week/IGeminiHabitGenerationService.cs`
```csharp
Task<Result<HabitSchedulingResult>> GenerateScheduleAsync(
    string goalAsStated, string deadlineAsStated,
    string baselineAnswersJson, DateTime startDate,
    CancellationToken ct = default);
```

---

## Phase 4 — Application Layer: Command Updates

### 4.1 `src/LifeGrid.Application/Onboarding/Commands/TriggerGoalValidationCommand.cs`

**Record:**
```csharp
public record TriggerGoalValidationCommand(DateTime ChosenStartDate)
    : IRequest<Result<IReadOnlyList<RefinementQuestionDto>>>;
```

**Handler** — after loading session and before calling Gemini:
```csharp
session.SetChosenStartDate(request.ChosenStartDate);
// (session is already being upserted at AdvanceToAwaitingValidation — the SetChosenStartDate
//  call precedes that upsert so the date is persisted in the same save)
```

Pass to Gemini:
```csharp
var validationResult = await gemini.ValidateGoalAsync(
    session.RawGoalDraft, request.ChosenStartDate, cancellationToken);
```

### 4.2 `src/LifeGrid.Application/Onboarding/Commands/FinalizeGoalCommand.cs`

Add guard after loading session:
```csharp
if (session.ChosenStartDate is null)
    return Result.Failure("No start date has been chosen for this goal.");
```

Change `Goal.Create()` call:
```csharp
var goal = GoalAggregate.Create(
    userProfile.UserId,
    dto.Description,
    dto.AmbientTag,
    dto.Duration,
    dto.DeadlineDate,
    startDate:    session.ChosenStartDate.Value,
    creationDate: DateTime.Now);
```

### 4.3 `src/LifeGrid.Application/Week/Commands/GenerateHabitsCommand.cs`

Pass `goal.StartDate` to service:
```csharp
var serviceResult = await habitGenerationService.GenerateScheduleAsync(
    goal.Description,
    goal.DeadlineDate.ToString("yyyy-MM-dd"),
    BuildBaselineAnswersJson(goal),
    goal.StartDate,           // NEW
    cancellationToken);
```

### 4.4 `src/LifeGrid.Application/Goal/RecalculateGoalScheduleCommand.cs`

Pass current-week Monday as start date:
```csharp
var currentMonday = GoalAggregate.CalculateStartDate(DateTime.UtcNow);
var serviceResult = await habitGenerationService.GenerateScheduleAsync(
    goal.Description,
    goal.DeadlineDate.ToString("yyyy-MM-dd"),
    baselineJson,
    currentMonday,            // NEW
    cancellationToken);
```

---

## Phase 5 — Infrastructure Layer

### 5.1 `src/LifeGrid.Infrastructure/AI/GeminiGoalValidationService.cs`

Update `ValidateGoalAsync` signature:
```csharp
public async Task<Result<GeminiValidationResult>> ValidateGoalAsync(
    string rawDraft, DateTime startDate, CancellationToken ct = default)
```

Replace prompt preprocessing — remove `$"[Current date: {today}]\n\n"` prefix; inject via template replacement:
```csharp
var prompt = Prompt1Template
    .Replace("${USER_INPUT_TEXT}", rawDraft)
    .Replace("${START_DATE}", startDate.ToString("MMMM d, yyyy"));
```

### 5.2 `src/LifeGrid.Infrastructure/AI/GeminiHabitGenerationService.cs`

Update `GenerateScheduleAsync` signature:
```csharp
public async Task<Result<HabitSchedulingResult>> GenerateScheduleAsync(
    string goalAsStated, string deadlineAsStated, string baselineAnswersJson,
    DateTime startDate, CancellationToken ct = default)
```

Replace Prompt 2.1 preprocessing — remove `$"[Current date: {today}]\n\n"` prefix:
```csharp
var prompt1 = Prompt21Template
    .Replace("${USER_GOAL}",                  goalAsStated)
    .Replace("${USER_DEADLINE}",              deadlineAsStated)
    .Replace("${USER_BASELINE_ANSWERS_JSON}", baselineAnswersJson)
    .Replace("${START_DATE}",                 startDate.ToString("MMMM d, yyyy"));
```

Prompt 2.2 is unchanged (receives start_date embedded in the raw Prompt 2.1 JSON).

### 5.3 Update embedded prompt files

**`src/LifeGrid.Infrastructure/AI/Prompts/prompt1.txt`**

Replace:
```
The current date for calculating relative deadlines (e.g., "in 6 months") is June 10, 2026.
```
With:
```
The goal start date (the first Monday the user will begin tracking) is ${START_DATE}.
All relative deadline calculations (e.g., "in 6 months") MUST use ${START_DATE} as the base date.
```

**`src/LifeGrid.Infrastructure/AI/Prompts/prompt2.1.txt`**

- Replace `The current baseline date is June 10, 2026.` with `The goal start date is ${START_DATE}.`
- Replace `based on June 10, 2026` / `relative to June 10, 2026` / similar phrases with `based on ${START_DATE}`.
- Add `"start_date": "${START_DATE}"` field to the Case A (infeasible) output JSON schema.
- Add `"start_date": "${START_DATE}"` field to the Case B (feasible) output JSON schema, directly after `"number_of_full_weeks"`.

**`src/LifeGrid.Infrastructure/AI/Prompts/prompt2.2.txt`**

- Replace `The current baseline date for calendar tracking is June 10, 2026.` with:
  `Use the start_date field from the input JSON as the starting date for Week 1. The start_date for Week 1 in your output MUST equal the input start_date value.`

Also update the corresponding **`docs/specs/assets/prompts/`** files to stay in sync with the embedded versions (spec reference only).

### 5.4 EF Core Entity Configurations

**`src/LifeGrid.Infrastructure/Data/EntityConfigurations/GoalConfiguration.cs`**
Add:
```csharp
builder.Property(e => e.CreationDate);
```

**`src/LifeGrid.Infrastructure/Data/EntityConfigurations/OnboardingSessionConfiguration.cs`**
Add:
```csharp
builder.Property(e => e.ChosenStartDate);
```

### 5.5 EF Core Migration

Generate migration **`AddGoalCreationDateAndSessionChosenStartDate`**:
```
dotnet ef migrations add AddGoalCreationDateAndSessionChosenStartDate \
  --project src/LifeGrid.Infrastructure \
  --startup-project src/LifeGrid.Presentation
```

The generated migration must produce:
- `migrationBuilder.AddColumn<DateTime>("CreationDate", "Goals", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")`
- `migrationBuilder.AddColumn<DateTime?>("ChosenStartDate", "OnboardingProgressCache", nullable: true)`

Verify the generated `Migration.cs` and adjust the `defaultValueSql` if EF Core generates a different default. The `Down()` method removes both columns.

---

## Phase 6 — Presentation Layer

### 6.1 `src/LifeGrid.Presentation/ViewModels/SetupViewModel.cs` (CreateGoalViewModel)

Add two properties above `IsEntryFlowVisible`:
```csharp
[ObservableProperty] private DateTime _chosenStartDate = GetCurrentWeekMonday();

public DateTime MinimumStartDate { get; } = GetCurrentWeekMonday();

private static DateTime GetCurrentWeekMonday()
{
    var today = DateTime.Today;
    var diff  = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
    return today.AddDays(diff);
}
```

Update `CompleteStep1Async()`:
```csharp
var result = await mediator.Send(new TriggerGoalValidationCommand(ChosenStartDate));
```

### 6.2 `src/LifeGrid.Presentation/Pages/SetupPage.xaml`

Inside State A (`IsRefinementActive, Converter={StaticResource InverseBool}` → `IsValidating, Converter=...`) section, add a date picker block **above** the goal text `Entry` and existing labels:

```xml
<!-- Goal start date picker -->
<Label Text="Choose your start date" />
<DatePicker
    x:Name="StartDatePicker"
    Date="{Binding ChosenStartDate, Mode=TwoWay}"
    MinimumDate="{Binding MinimumStartDate}"
    Format="ddd, MMM d yyyy"
    DateSelected="OnStartDateSelected" />
<Label
    Text="Only Mondays are valid start dates"
    FontSize="11"
    TextColor="{StaticResource Secondary}" />
```

### 6.3 `src/LifeGrid.Presentation/Pages/SetupPage.xaml.cs`

Add event handler:
```csharp
private void OnStartDateSelected(object sender, DateChangedEventArgs e)
{
    if (e.NewDate.DayOfWeek == DayOfWeek.Monday) return;
    // Snap to next Monday
    var diff   = ((int)DayOfWeek.Monday - (int)e.NewDate.DayOfWeek + 7) % 7;
    var monday = e.NewDate.AddDays(diff);
    if (sender is DatePicker dp)
        dp.Date = monday;
}
```

---

## Phase 7 — Build & Test

```
dotnet build LifeGrid.slnx   → 0 errors
dotnet test                  → all Phase 14 tests pass + all prior 245 pass
```

---

## Execution Order Summary

| Step | Layer | Artifact |
|---|---|---|
| 1a | Tests | `GoalStartDateTests.cs` — remove 3 old, add 2 new |
| 1b | Tests | `GoalTests.cs`, `GoalDomainMutationTests.cs`, `AbandonGoalCommandHandlerTests.cs`, `RecalculateGoalScheduleCommandHandlerTests.cs`, `GetTimelineQueryHandlerTests.cs`, `GetGoalsQueryHandlerTests.cs` — update `Goal.Create()` call sites |
| 1c | Tests | `TriggerGoalValidationCommandHandlerTests.cs` — update 7 + add 2 |
| 1d | Tests | `FinalizeGoalCommandHandlerTests.cs` — update setup + add 1 |
| 1e | Tests | `GenerateHabitsCommandTests.cs` — update mocks + add 1 |
| 1f | Tests | `GeminiGoalValidationServiceTests.cs`, `GeminiHabitGenerationServiceTests.cs` — update signatures |
| 2 | Domain | `Goal.cs` (Create signature + CreationDate), `OnboardingSession.cs` (ChosenStartDate) |
| 3 | Application interfaces | `IGeminiGoalValidationService`, `IGeminiHabitGenerationService` |
| 4 | Application commands | `TriggerGoalValidationCommand`, `FinalizeGoalCommand`, `GenerateHabitsCommand`, `RecalculateGoalScheduleCommand` |
| 5a | Infrastructure | `GeminiGoalValidationService`, `GeminiHabitGenerationService` |
| 5b | Infrastructure | Prompt files (`prompt1.txt`, `prompt2.1.txt`, `prompt2.2.txt`) — embedded + docs copies |
| 5c | Infrastructure | `GoalConfiguration`, `OnboardingSessionConfiguration` |
| 5d | Infrastructure | EF Core migration `AddGoalCreationDateAndSessionChosenStartDate` |
| 6 | Presentation | `SetupViewModel.cs` (ChosenStartDate + MinimumStartDate), `SetupPage.xaml` (DatePicker), `SetupPage.xaml.cs` (snap handler) |
| 7 | Verify | `dotnet build` + `dotnet test` |
