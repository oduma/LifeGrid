# Phase 10 — Temporal Alignment & Week Synchronization
## Implementation Plan

**Status:** DONE — completed 2026-06-21

**Finalized requirements:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` § Phase 10 (P10.1–P10.6)

---

## Pre-Flight Analysis (LEAD_ARCHITECT)

### Scope
Three domain rules; no UI changes; no new bounded context. Changes span:
- `LifeGrid.Domain` — two entities modified
- `LifeGrid.Application` — one interface extended, two command handlers modified
- `LifeGrid.Infrastructure` — two EF configurations updated, one repository extended, one migration added
- All three test projects — compilation fixes + new test files

### Key Invariants
1. `Goal.StartDate` is always a Monday. Monday creation → same day; any other day → next Monday.
2. `Week` table has at most one row per unique `StartDate`.
3. `WeekGoal.WeekGoalNumber` is 1-based, scoped to a `GoalId`, ordered by schedule sequence.

### Breaking Changes
Changing `Goal.Create()` and `WeekGoal.Create()` signatures is a compile-time break. Every call site must be updated in the same pass. All affected files are catalogued in Phase D.

---

## Phase A — Domain Layer

### A1 · `src/LifeGrid.Domain/Goal/Goal.cs`

**Changes:**
1. Add `public DateTime StartDate { get; private set; }` property.
2. Add public static method:
   ```csharp
   public static DateTime CalculateStartDate(DateTime creationDate)
   {
       var day = creationDate.Date;
       int daysToMonday = ((int)DayOfWeek.Monday - (int)day.DayOfWeek + 7) % 7;
       return day.AddDays(daysToMonday);
   }
   ```
3. Add `DateTime creationDate` parameter to `Goal.Create()` after `deadlineDate`.
4. Inside `Create()`, set `StartDate = CalculateStartDate(creationDate)`.

**TDD trigger:** Write `GoalStartDateTests.cs` (Phase D1) BEFORE this code; tests drive the formula.

---

### A2 · `src/LifeGrid.Domain/WeekGoal/WeekGoal.cs`

**Changes:**
1. Add `public int WeekGoalNumber { get; private set; }` property.
2. Add `int weekGoalNumber` parameter to `WeekGoal.Create()` after `goalId`.
3. Assign `WeekGoalNumber = weekGoalNumber` inside `Create()`.

---

## Phase B — Application Layer

### B1 · `src/LifeGrid.Application/Week/IWeekRepository.cs`

Add two new method signatures (keep existing `AddAsync`):
```csharp
Task<WeekEntity?> GetByStartDateAsync(DateTime startDate, CancellationToken ct = default);
Task AddWeekGoalAsync(WeekGoalEntity weekGoal, CancellationToken ct = default);
```

---

### B2 · `src/LifeGrid.Application/Onboarding/Commands/FinalizeGoalCommand.cs`

**Change:** Line 68–73. Pass `DateTime.Now` as the new `creationDate` argument:
```csharp
var goal = GoalAggregate.Create(
    userProfile.UserId,
    dto.Description,
    dto.AmbientTag,
    dto.Duration,
    dto.DeadlineDate,
    DateTime.Now);       // ← NEW: creationDate for StartDate calculation
```

---

### B3 · `src/LifeGrid.Application/Week/Commands/GenerateHabitsCommand.cs`

**Change:** Replace the existing `foreach` loop (lines 71–85) with the deduplication + sequencing version:

```csharp
int weekGoalNumber = 0;
foreach (var weekDto in feasible.Schedule)
{
    weekGoalNumber++;

    var existingWeek = await weekRepository.GetByStartDateAsync(weekDto.StartDate, cancellationToken);
    WeekGoalEntity weekGoal;

    if (existingWeek is null)
    {
        var newWeek = WeekEntity.Create(weekDto.WeekNumber, weekDto.StartDate);
        weekGoal    = WeekGoalEntity.Create(newWeek.WeekId, goal.GoalId, weekGoalNumber);
        await weekRepository.AddAsync(newWeek, weekGoal, cancellationToken);
    }
    else
    {
        weekGoal = WeekGoalEntity.Create(existingWeek.WeekId, goal.GoalId, weekGoalNumber);
        await weekRepository.AddWeekGoalAsync(weekGoal, cancellationToken);
    }

    var weekDeadline = weekDto.StartDate.AddDays(6);
    var habits = weekDto.Habits
        .Select(h => HabitEntity.Create(
            weekGoal.WeekGoalId, h.Description, h.Description,
            h.Value, h.Unit, weekDeadline))
        .ToList();

    await habitRepository.AddRangeAsync(habits, cancellationToken);
}
```

---

## Phase C — Infrastructure Layer

### C1 · `src/LifeGrid.Infrastructure/Data/EntityConfigurations/GoalConfiguration.cs`

Add after the `DeadlineDate` property mapping:
```csharp
builder.Property(e => e.StartDate);
```

---

### C2 · `src/LifeGrid.Infrastructure/Data/EntityConfigurations/WeekGoalConfiguration.cs`

Add after the `GoalId` property mapping:
```csharp
builder.Property(wg => wg.WeekGoalNumber);
```

---

### C3 · `src/LifeGrid.Infrastructure/Data/Repositories/WeekRepository.cs`

Add two method implementations to the existing class:
```csharp
public Task<WeekEntity?> GetByStartDateAsync(DateTime startDate, CancellationToken ct = default)
    => db.Weeks.FirstOrDefaultAsync(w => w.StartDate.Date == startDate.Date, ct);

public Task AddWeekGoalAsync(WeekGoalEntity weekGoal, CancellationToken ct = default)
{
    db.WeekGoals.Add(weekGoal);
    return Task.CompletedTask;
}
```

---

### C4 · EF Core Migration `Phase10_TemporalAlignment`

**Step 1:** Run `dotnet ef migrations add Phase10_TemporalAlignment --project src/LifeGrid.Infrastructure --startup-project src/LifeGrid.Presentation` to scaffold the migration.

**Step 2:** Edit the generated `Up()` method to:

```csharp
migrationBuilder.AddColumn<DateTime>(
    name:         "StartDate",
    table:        "Goals",
    nullable:     false,
    defaultValue: new DateTime(2026, 6, 23));   // Monday of migration week

migrationBuilder.AddColumn<int>(
    name:         "WeekGoalNumber",
    table:        "WeekGoals",
    nullable:     false,
    defaultValue: 0);

// Backfill WeekGoalNumber with sequential values per GoalId ordered by Week.StartDate
migrationBuilder.Sql(@"
    UPDATE WeekGoals SET WeekGoalNumber = (
        SELECT rn FROM (
            SELECT wg.WeekGoalId,
                   ROW_NUMBER() OVER (
                       PARTITION BY wg.GoalId
                       ORDER BY (SELECT w.StartDate FROM Weeks w WHERE w.WeekId = wg.WeekId)
                   ) AS rn
            FROM WeekGoals wg
        ) sub
        WHERE sub.WeekGoalId = WeekGoals.WeekGoalId
    );
");
```

**`Down()` method:** Drop both columns in reverse order.

---

## Phase D — Tests

### D1 · NEW: `tests/LifeGrid.Domain.Tests/Goal/GoalStartDateTests.cs`

Write BEFORE modifying `Goal.cs`.

**Tests:**
- Theory with 7 inline cases covering all days of the week → expected Monday:
  ```
  (2026-06-15 Sun → 2026-06-16 Mon)
  (2026-06-16 Mon → 2026-06-16 Mon)  ← same-day rule
  (2026-06-17 Tue → 2026-06-23 Mon)
  (2026-06-18 Wed → 2026-06-23 Mon)
  (2026-06-19 Thu → 2026-06-23 Mon)
  (2026-06-20 Fri → 2026-06-23 Mon)
  (2026-06-21 Sat → 2026-06-23 Mon)
  ```
- Fact: `Goal.Create(..., creationDate)` stores the `StartDate` on the resulting aggregate.
- Fact: Returned `StartDate` is always a `DayOfWeek.Monday`.

---

### D2 · UPDATE: `tests/LifeGrid.Domain.Tests/Goal/GoalTests.cs`

In `BuildGoal()`, add `DateTime.Now` as the 6th argument to `Goal.Create()`. No logic changes needed.

---

### D3 · UPDATE: `tests/LifeGrid.Application.Tests/Habit/GenerateHabitsCommandTests.cs`

- In `SampleGoal()`, add `new DateTime(2026, 6, 16)` (a Monday) as `creationDate` to `GoalAggregate.Create()`.
- Add new test: `ExistingWeekForStartDate_ReusesWeekId_CallsAddWeekGoalAsync`
  - Arrange: `_weekRepo.GetByStartDateAsync(...)` returns a pre-existing `WeekEntity`.
  - Assert: `_weekRepo.DidNotReceive().AddAsync(Arg.Any<WeekEntity>(), Arg.Any<WeekGoalEntity>(), ...)`.
  - Assert: `_weekRepo.Received(1).AddWeekGoalAsync(Arg.Any<WeekGoalEntity>(), ...)`.
- Add new test: `WeekGoalNumber_IsOneForSingleWeekSchedule`
  - Capture the `WeekGoalEntity` passed to `AddAsync` via `Arg.Do<WeekGoalEntity>(...)`.
  - Assert: `capturedWeekGoal.WeekGoalNumber == 1`.

---

### D4 · UPDATE: `tests/LifeGrid.Application.Tests/Onboarding/FinalizeGoalCommandHandlerTests.cs`

In the `ValidFlow_CreatesGoalAndRefinementAnswers` test, add:
```csharp
savedGoal!.StartDate.Should().Be(Goal.CalculateStartDate(DateTime.Now));
```
(Use `Goal.CalculateStartDate(DateTime.Now)` so the assertion is deterministic regardless of test run time.)

---

### D5 · UPDATE: `tests/LifeGrid.Infrastructure.Tests/Data/WeekRepositoryTests.cs`

- In `SeedGoalAsync()`: add `DateTime.Now` as 6th arg to `Goal.Create()`.
- In all `WeekGoal.Create(week.WeekId, goalId)` calls: add `1` as `weekGoalNumber`.
- Rename tests that call `repository.AddAsync(week, weekGoal)` to keep passing — no mock change needed since `AddAsync` signature is unchanged.
- Add new test: `GetByStartDateAsync_WhenWeekExists_ReturnsIt`
  - Persist a `Week` with a known `StartDate`, then query `GetByStartDateAsync` for that date. Assert non-null.
- Add new test: `GetByStartDateAsync_WhenNoneExists_ReturnsNull`
  - Query for a date with no matching `Week`. Assert null.
- Add new test: `AddWeekGoalAsync_PersistsAfterCommit`
  - Persist a `Week` manually. Call `AddWeekGoalAsync`. Commit. Assert `_db.WeekGoals.Count() == 1`.

---

### D6 · UPDATE: `tests/LifeGrid.Infrastructure.Tests/Schema/GoalSchemaTests.cs`

- Line 47: Add `DateTime.Now` as 6th arg to `Goal.Create()`.
- In `Goal_CanBeWrittenAndReadBack`, add assertion: `stored!.StartDate.Should().Be(Goal.CalculateStartDate(DateTime.Now))`.

---

### D7 · NEW: `tests/LifeGrid.Infrastructure.Tests/Data/WeekDeduplicationTests.cs`

Full integration test (in-memory SQLite, `Migrate()`):

```
Arrange:
  - Seed two goals (GoalA, GoalB) with distinct GoalIds.
  - Create a Monday StartDate, e.g. new DateTime(2026, 6, 23).
  - Persist one Week for that StartDate.
  - Create WeekGoal1 for GoalA linked to that Week's WeekId; WeekGoalNumber = 1.
  - Create WeekGoal2 for GoalB linked to the SAME Week's WeekId; WeekGoalNumber = 1.
  - Commit.

Assert:
  - _db.Weeks.Count() == 1  (no duplicate Week row)
  - Both WeekGoals have WeekId == the shared WeekId
  - WeekGoal1.WeekGoalNumber == 1
  - WeekGoal2.WeekGoalNumber == 1  (per-goal sequence; both are first week of their respective goals)
```

---

## Execution Order

```
D1 (write failing tests) →
A1 (Goal.StartDate) →
A2 (WeekGoal.WeekGoalNumber) →
D2, D3 (fix compilation in tests) →
B1 (IWeekRepository interface) →
B2 (FinalizeGoalCommand) →
B3 (GenerateHabitsCommand) →
C1, C2 (EF config) →
C3 (WeekRepository impl) →
C4 (migration scaffold + edit + backfill SQL) →
D4, D5, D6 (update existing tests) →
D7 (new dedup integration test) →
dotnet build → dotnet test
```

---

## Estimated Test Delta

| Test project | New tests | Updated tests |
|---|---|---|
| Domain | ~10 (GoalStartDateTests) | 1 (GoalTests) |
| Application | ~2 (GenerateHabitsCommandTests additions) | 2 (SampleGoal + ValidFlow) |
| Infrastructure | ~5 (WeekRepositoryTests additions + WeekDeduplicationTests) | 2 (SeedGoalAsync + GoalSchemaTests) |
| **Total new** | **~17** | |

Estimated totals: 193 existing + ~17 new = **~210 tests**.

---

## Post-Implementation Results

**Build:** 0 errors. 3 pre-existing `CS0612` warnings on MAUI-generated code (`ViceSurveyPage.xaml`) — not Phase 10.

**Tests:** 213 passing (83 Domain + 66 Application + 64 Infrastructure). +20 new tests vs. estimated +17.

| Test project | New tests (actual) |
|---|---|
| Domain | 11 (`GoalStartDateTests`: 7 Theory + 4 Facts) |
| Application | 5 (`GenerateHabitsCommandTests`: 2 new + 1 assertion; `FinalizeGoalCommandHandlerTests`: 1 new assertion) |
| Infrastructure | 6 (`WeekRepositoryTests`: 3 new; `WeekDeduplicationTests`: 3 new) |
| **Total new** | **20** |

**Call-site updates:** 13 test files updated for `Goal.Create()` / `WeekGoal.Create()` signature changes (initial plan listed 4 — actual scope was much wider due to Vice/HUD tests that also reference these entities).

**Implementation Notes (resolved)**

| Issue | Resolution |
|---|---|
| Test date error: initial plan draft used `2026-06-16` as "Monday" | `2026-06-16` is Tuesday; `2026-06-15` is the actual Monday. Formula was correct; `InlineData` fixtures corrected. |
| Migration scaffolded with `DateTime(1,1,1)` default for `StartDate` | Manually edited to `DateTime(2026, 6, 23)` (Monday of migration week) after scaffold. |
| `--startup-project src/LifeGrid.Presentation` fails for EF CLI | Used `--startup-project src/LifeGrid.Infrastructure` with `--framework net10.0` instead. |
