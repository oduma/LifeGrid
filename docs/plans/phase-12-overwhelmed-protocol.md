# Phase 12 — Goal Card Actions: The Overwhelmed Protocol
**Status:** DONE — completed 2026-06-21

## Source Documents
- Requirements: `docs/requirements/Phase-12-requirements.md`
- Finalized spec: `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` §P12
- Wireframes: `docs/specs/assets/wireframes/wf12.png` (Option A) · `wf13.png` (Option B)
- AI Prompts: `docs/specs/assets/prompts/prompt2.1.txt` · `prompt2.2.txt`

---

## Clarification Answers
| # | Question | Answer |
|---|---|---|
| Q1 | Duration string after extend | **A** — recalculate from date arithmetic |
| Q2 | XP floor on Abandon | **A** — floor at 0, never negative |
| Q3 | "Unstarted" WeekGoal scope | **A** — Week.StartDate strictly after today (current week preserved) |
| Q4 | Gemini baseline for Option B | **A** — original GoalRefinementAnswers + overwhelmed comment appended |
| Q5 | Navigation after resolution | **A** — navigate back to Goals list and refresh |

---

## Architecture Overview

```
Left Swipe  → [GoalsPage SwipeView] → AbandonGoalSwipeCommand (ViewModel)
                → DisplayAlert (XP calculation)
                → AbandonGoalCommand (MediatR)
                    ├─ Domain: Goal.MarkAbandoned()
                    ├─ Domain: UserProfile.DeductXp(historicalXP + 100)
                    ├─ Infra: delete future Habits + WeekGoals
                    └─ CommitAsync
                → GoToAsync("..") + LoadAsync()

Right Swipe → [GoalsPage SwipeView] → ExtendScheduleSwipeCommand (ViewModel)
                → GoToAsync("overwhelmed-recalculate?goalId=...")
                → [OverwhelmedRecalculatePage]
                    → RecalculateGoalScheduleCommand (MediatR)
                        ├─ Domain: Goal.ExtendDeadlineByPercent(25)
                        ├─ Infra: delete future Habits + WeekGoals
                        ├─ AI: IGeminiHabitGenerationService (2-stage pipeline)
                        ├─ Infra: insert new WeekGoal + Habit rows
                        ├─ Domain: UserProfile.DeductXp(100)
                        └─ CommitAsync
                    → GoToAsync("../..") + LoadAsync()
```

---

## Phase 1 — Tests (TDD: write first)

### 1.1 `UserEconomyDeductXpTests.cs` (Domain.Tests)
File: `tests/LifeGrid.Domain.Tests/UserProfile/UserEconomyDeductXpTests.cs`
- `DeductXp_NormalAmount_ReducesLifetimeXp` — start 500, deduct 200 → 300
- `DeductXp_ExceedsBalance_FloorsAtZero` — start 100, deduct 300 → 0
- `DeductXp_ZeroAmount_NoChange` — start 500, deduct 0 → 500

### 1.2 `GoalDomainMutationTests.cs` (Domain.Tests)
File: `tests/LifeGrid.Domain.Tests/Goal/GoalDomainMutationTests.cs`
- `MarkAbandoned_SetsStatusToAbandoned`
- `ExtendDeadlineByPercent_100DayGoal_AddsExactly25Days` ← TDD invariant from spec
- `ExtendDeadlineByPercent_UpdatesDurationString`

### 1.3 `GetGoalHistoricalXpQueryHandlerTests.cs` (Application.Tests)
File: `tests/LifeGrid.Application.Tests/Goal/GetGoalHistoricalXpQueryHandlerTests.cs`
- `NoWeekGoals_ReturnsZero`
- `WithWeekGoals_ReturnsCorrectSum` — two WeekGoals with 200 + 250 XP → 450

### 1.4 `AbandonGoalCommandHandlerTests.cs` (Application.Tests)
File: `tests/LifeGrid.Application.Tests/Goal/AbandonGoalCommandHandlerTests.cs`
- `GoalNotFound_ReturnsFailure`
- `AbandonGoal_SetsStatusAbandoned`
- `AbandonGoal_450XpEarned_Deducts550FromLifetimeXp` ← TDD invariant from spec (§4)
- `AbandonGoal_PenaltyExceedsBalance_FloorsAtZero` — lifetime XP 300, penalty 550 → 0
- `AbandonGoal_DeletesFutureWeekGoalsAndHabits`

### 1.5 `RecalculateGoalScheduleCommandHandlerTests.cs` (Application.Tests)
File: `tests/LifeGrid.Application.Tests/Goal/RecalculateGoalScheduleCommandHandlerTests.cs`
- `GoalNotFound_ReturnsFailure`
- `ExtendDeadline_100DayGoal_AddsExactly25Days` ← TDD invariant (§4)
- `FlatPenalty_DeductsOnly100Xp_NotHistoricalXp` ← TDD invariant (§4)
- `GeminiInfeasible_ReturnsFailureWithoutCommit`
- `GeminiFeasible_InsertsNewWeekGoalsAndHabits`

---

## Phase 2 — Domain Layer

### 2.1 `src/LifeGrid.Domain/Goal/Goal.cs`
Add two mutation methods:
```
void MarkAbandoned()
    → Status = GoalStatus.Abandoned

void ExtendDeadlineByPercent(double percent)
    → extensionDays = (DeadlineDate - StartDate).TotalDays * (percent / 100.0)
    → DeadlineDate = DeadlineDate.AddDays(Math.Round(extensionDays))
    → totalDays = (int)Math.Round((DeadlineDate - StartDate).TotalDays)
    → Duration = totalDays >= 30
          ? $"{(int)Math.Round(totalDays / 30.44)} months"
          : $"{(int)Math.Ceiling(totalDays / 7.0)} weeks"
```

### 2.2 `src/LifeGrid.Domain/UserProfile/UserEconomy.cs`
Add:
```
internal void DeductXp(int amount)
    → LifetimeXp = Math.Max(0, LifetimeXp - amount)
```

### 2.3 `src/LifeGrid.Domain/UserProfile/UserProfile.cs`
Add:
```
public void DeductXp(int amount) => Economy.DeductXp(amount);
```

---

## Phase 3 — Application Layer: Interfaces

### 3.1 `src/LifeGrid.Application/Goal/IGoalRepository.cs`
Add:
```
Task<GoalAggregate?> GetByIdAsync(Guid goalId, CancellationToken ct = default);
```

### 3.2 `src/LifeGrid.Application/Week/IWeekRepository.cs`
Add three methods:
```
Task<IReadOnlyList<WeekGoalEntity>> GetFutureWeekGoalsByGoalIdAsync(
    Guid goalId, DateTime afterDate, CancellationToken ct = default);

Task RemoveWeekGoalRangeAsync(
    IReadOnlyList<WeekGoalEntity> weekGoals, CancellationToken ct = default);

Task<int> GetMaxWeekGoalNumberAsync(
    Guid goalId, CancellationToken ct = default);
```

### 3.3 `src/LifeGrid.Application/Habit/IHabitRepository.cs`
Add:
```
Task RemoveByWeekGoalIdsAsync(
    IReadOnlyList<Guid> weekGoalIds, CancellationToken ct = default);
```

---

## Phase 4 — Application Layer: Queries & Commands

### 4.1 `src/LifeGrid.Application/Goal/GetGoalHistoricalXpQuery.cs`
```
public record GetGoalHistoricalXpQuery(Guid GoalId) : IRequest<Result<int>>;

Handler:
    → db.WeekGoals.Where(wg => wg.GoalId == request.GoalId)
                  .SumAsync(wg => wg.GoalWeeklyXpEarned, ct)
    → return Result<int>.Success(sum)
```
Dependency: `IWeekRepository` (or direct `IUnitOfWork` query — follow existing pattern of injecting repo interfaces, not DbContext).

Actually this requires a read from WeekGoals summing XP. We'll add `GetHistoricalXpByGoalIdAsync(Guid goalId)` to `IWeekRepository` as a 4th addition in Phase 3.2, or handle it within the command handler via a dedicated query. Plan to add to IWeekRepository for consistency.

**Revised Phase 3.2** — also add:
```
Task<int> GetHistoricalXpByGoalIdAsync(Guid goalId, CancellationToken ct = default);
```

### 4.2 `src/LifeGrid.Application/Goal/AbandonGoalCommand.cs`
```
public record AbandonGoalCommand(Guid GoalId) : IRequest<Result>;

Handler dependencies:
    IUserProfileRepository, IGoalRepository, IWeekRepository,
    IHabitRepository, IUnitOfWork

Algorithm:
    1. Load userProfile → load goal by GoalId → fail if either null
    2. Get historicalXp = weekRepository.GetHistoricalXpByGoalIdAsync(GoalId)
    3. goal.MarkAbandoned()
    4. futureWeekGoals = weekRepository.GetFutureWeekGoalsByGoalIdAsync(GoalId, DateTime.UtcNow.Date)
    5. habitRepository.RemoveByWeekGoalIdsAsync(futureWeekGoals.Select(wg => wg.WeekGoalId).ToList())
    6. weekRepository.RemoveWeekGoalRangeAsync(futureWeekGoals)
    7. userProfile.DeductXp(historicalXp + 100)
    8. await unitOfWork.CommitAsync(ct)
    9. return Result.Success()
```

### 4.3 `src/LifeGrid.Application/Goal/RecalculateGoalScheduleCommand.cs`
```
public record RecalculateGoalScheduleCommand(
    Guid GoalId, string OverwhelmedComment) : IRequest<Result>;

Handler dependencies:
    IUserProfileRepository, IGoalRepository, IWeekRepository,
    IHabitRepository, IGeminiHabitGenerationService, IUnitOfWork

Algorithm:
    1. Load userProfile → load goal by GoalId → fail if either null
    2. goal.ExtendDeadlineByPercent(25.0)   // mutates DeadlineDate + Duration
    3. futureWeekGoals = weekRepository.GetFutureWeekGoalsByGoalIdAsync(GoalId, DateTime.UtcNow.Date)
    4. habitRepository.RemoveByWeekGoalIdsAsync(futureWeekGoals.Select(wg => wg.WeekGoalId).ToList())
    5. weekRepository.RemoveWeekGoalRangeAsync(futureWeekGoals)
    6. baselineJson = BuildBaselineJson(goal, overwhelmedComment)
       → serialize RefinementAnswers ordered by RankOrder as [{question, answer}]
       → append { question: "Why are you overwhelmed?", answer: overwhelmedComment }
    7. serviceResult = await habitGenerationService.GenerateScheduleAsync(
           goal.Description, goal.DeadlineDate.ToString("yyyy-MM-dd"), baselineJson, ct)
    8. If !serviceResult.IsSuccess → return Failure (no commit)
    9. If Infeasible → return Failure (no commit)
   10. maxWeekGoalNum = weekRepository.GetMaxWeekGoalNumberAsync(GoalId)
   11. For each WeekScheduleDto (same loop pattern as GenerateHabitsCommand):
           - GetByStartDateAsync / create Week if missing
           - Create WeekGoal (WeekGoalNumber = maxWeekGoalNum + i)
           - Create Habit rows
   12. userProfile.DeductXp(100)
   13. await unitOfWork.CommitAsync(ct)
   14. return Result.Success()
```

---

## Phase 5 — Infrastructure Layer

### 5.1 `src/LifeGrid.Infrastructure/Data/Repositories/GoalRepository.cs`
Add:
```csharp
public Task<GoalAggregate?> GetByIdAsync(Guid goalId, CancellationToken ct = default)
    => db.Goals.FirstOrDefaultAsync(g => g.GoalId == goalId, ct);
```

### 5.2 `src/LifeGrid.Infrastructure/Data/Repositories/WeekRepository.cs`
Add four methods:
```csharp
// Future WeekGoals for this goal
public async Task<IReadOnlyList<WeekGoalEntity>> GetFutureWeekGoalsByGoalIdAsync(
    Guid goalId, DateTime afterDate, CancellationToken ct = default)
    => await db.WeekGoals
               .Where(wg => wg.GoalId == goalId &&
                            db.Weeks.Any(w => w.WeekId == wg.WeekId && w.StartDate > afterDate))
               .ToListAsync(ct);

// Mark for removal (no SaveChanges — called before CommitAsync)
public Task RemoveWeekGoalRangeAsync(
    IReadOnlyList<WeekGoalEntity> weekGoals, CancellationToken ct = default)
{
    db.WeekGoals.RemoveRange(weekGoals);
    return Task.CompletedTask;
}

// Max existing week goal number (0 if none remain after pruning)
public Task<int> GetMaxWeekGoalNumberAsync(Guid goalId, CancellationToken ct = default)
    => db.WeekGoals
         .Where(wg => wg.GoalId == goalId)
         .Select(wg => (int?)wg.WeekGoalNumber)
         .MaxAsync(ct)
         .ContinueWith(t => t.Result ?? 0, ct);

// Sum of XP historically earned for this goal
public Task<int> GetHistoricalXpByGoalIdAsync(Guid goalId, CancellationToken ct = default)
    => db.WeekGoals
         .Where(wg => wg.GoalId == goalId)
         .SumAsync(wg => wg.GoalWeeklyXpEarned, ct);
```

### 5.3 `src/LifeGrid.Infrastructure/Data/Repositories/HabitRepository.cs`
Add:
```csharp
public async Task RemoveByWeekGoalIdsAsync(
    IReadOnlyList<Guid> weekGoalIds, CancellationToken ct = default)
{
    var habits = await db.Habits
        .Where(h => weekGoalIds.Contains(h.WeekGoalId))
        .ToListAsync(ct);
    db.Habits.RemoveRange(habits);
}
```

### 5.4 No new EF Core migration required
No new tables or columns are introduced in Phase 12. All mutations target existing columns (`Goal.Status`, `Goal.DeadlineDate`, `Goal.Duration`, `UserEconomy.LifetimeXp`) which EF Core change tracking handles automatically.

---

## Phase 6 — Presentation Layer

### 6.1 `src/LifeGrid.Presentation/ViewModels/GoalsViewModel.cs`
Add two relay commands:
```
[RelayCommand]
private async Task AbandonGoalSwipeAsync(Guid goalId):
    1. send GetGoalHistoricalXpQuery(goalId) → xp
    2. DisplayAlert "Warning: You will lose {xp + 100} XP ..." with Abandon/Cancel
    3. If cancelled → return
    4. send AbandonGoalCommand(goalId)
    5. await LoadAsync()

[RelayCommand]
private async Task ExtendScheduleSwipeAsync(Guid goalId):
    1. GoToAsync($"overwhelmed-recalculate?goalId={goalId}")
```

### 6.2 `src/LifeGrid.Presentation/Pages/GoalsPage.xaml`
Wrap the existing `Border` card in a `SwipeView`:
```xml
<SwipeView>
    <SwipeView.LeftItems>   <!-- activated by swiping left -->
        <SwipeItems>
            <SwipeItem Text="ABANDON"
                       BackgroundColor="#FF1B77"
                       Command="{Binding AbandonGoalSwipeCommand, Source={RelativeSource AncestorType={x:Type vm:GoalsViewModel}}}"
                       CommandParameter="{Binding GoalId}" />
        </SwipeItems>
    </SwipeView.LeftItems>
    <SwipeView.RightItems>  <!-- activated by swiping right -->
        <SwipeItems>
            <SwipeItem Text="EXTEND"
                       BackgroundColor="#a20ba0"
                       Command="{Binding ExtendScheduleSwipeCommand, Source={RelativeSource AncestorType={x:Type vm:GoalsViewModel}}}"
                       CommandParameter="{Binding GoalId}" />
        </SwipeItems>
    </SwipeView.RightItems>
    <!-- existing Border card content (unchanged) -->
</SwipeView>
```
SwipeItems only appear for Active goals — use `IsEnabled` binding via a `StatusToIsSwipeableConverter` (bool) or trigger on the DataTemplate.

### 6.3 New: `src/LifeGrid.Presentation/ViewModels/OverwhelmedRecalculateViewModel.cs`
```
[QueryProperty("GoalId", "goalId")]
public partial class OverwhelmedRecalculateViewModel(IMediator mediator) : ObservableObject

Properties:
    [ObservableProperty] Guid goalId
    [ObservableProperty] string overwhelmedComment = string.Empty
    [ObservableProperty] bool isLoading

[RelayCommand]
private async Task RecalculateAsync():
    IsLoading = true
    var result = await mediator.Send(new RecalculateGoalScheduleCommand(GoalId, OverwhelmedComment))
    IsLoading = false
    if (!result.IsSuccess) { show alert } else { GoToAsync("../..") }
```

### 6.4 New: `src/LifeGrid.Presentation/Pages/OverwhelmedRecalculatePage.xaml` + `.xaml.cs`
Layout (Clean Pixel Neon aesthetic):
- Page title: "Overwhelmed"
- Section header (DM Mono): "Why are you overwhelmed?"
- Multi-line `Editor` bound to `OverwhelmedComment` (Share Tech Mono, minimum height ~120)
- Warning `Label` (Error color): "Extending the deadline by 25% will cost 100 XP."
- Primary `Button` "Recalculate Schedule" — `IsEnabled="{Binding IsLoading, Converter={StaticResource InvertedBoolConverter}}"`, `Command="{Binding RecalculateCommand}"`
- `ActivityIndicator` — `IsRunning="{Binding IsLoading}"`, `IsVisible="{Binding IsLoading}"`

### 6.5 Registration
In `src/LifeGrid.Presentation/MauiProgram.cs`:
```csharp
builder.Services.AddTransient<OverwhelmedRecalculateViewModel>();
builder.Services.AddTransient<OverwhelmedRecalculatePage>();
```

In `src/LifeGrid.Presentation/AppShell.xaml` (or code-behind):
```csharp
Routing.RegisterRoute("overwhelmed-recalculate", typeof(OverwhelmedRecalculatePage));
```

---

## Phase 7 — Build & Test

```
dotnet build LifeGrid.slnx   → 0 errors
dotnet test                  → all Phase 12 tests pass + all prior 217 pass
```

---

## Execution Order Summary

| Step | Layer | Artifact |
|---|---|---|
| 1 | Tests | `UserEconomyDeductXpTests`, `GoalDomainMutationTests`, `GetGoalHistoricalXpQueryHandlerTests`, `AbandonGoalCommandHandlerTests`, `RecalculateGoalScheduleCommandHandlerTests` |
| 2 | Domain | `Goal.cs`, `UserEconomy.cs`, `UserProfile.cs` |
| 3 | Application interfaces | `IGoalRepository`, `IWeekRepository` (+4 methods), `IHabitRepository` (+1 method) |
| 4 | Application commands | `GetGoalHistoricalXpQuery.cs`, `AbandonGoalCommand.cs`, `RecalculateGoalScheduleCommand.cs` |
| 5 | Infrastructure | `GoalRepository`, `WeekRepository` (+4 methods), `HabitRepository` (+1 method) |
| 6 | Presentation | `GoalsViewModel` (+2 commands), `GoalsPage.xaml` (SwipeView wrap), `OverwhelmedRecalculateViewModel`, `OverwhelmedRecalculatePage`, registrations |
| 7 | Verify | `dotnet build` + `dotnet test` |

---

## Post-Implementation Results

**Build:** 0 errors · 3 pre-existing warnings (`ViceSurveyPage.xaml` CS0612 — unchanged)
**Tests:** 245 total — all pass (94 Domain + 87 Application + 64 Infrastructure; +28 new)

### Corrections applied during implementation

1. **`CharacterSpacing` not `LetterSpacing`** — `LetterSpacing` does not exist on MAUI `Label`/`Button`. Corrected to `CharacterSpacing` in two places in `OverwhelmedRecalculatePage.xaml`.
2. **`DisplayAlertAsync` not `DisplayAlert`** — `DisplayAlert` is `[Obsolete]` in MAUI .NET 10. All three call sites in `GoalsViewModel.cs` and `OverwhelmedRecalculateViewModel.cs` updated.
3. **`GrantXp` added to domain** — `UserEconomy.GrantXp` / `UserProfile.GrantXp` added alongside `DeductXp`. Legitimate domain operation (future habit-completion XP award) and required for clean test setup without bypassing encapsulation.
4. **`UpdateAsync` not needed** — EF Core change tracking detects `Goal` mutations automatically on `CommitAsync`; no explicit `UpdateAsync` was required.
5. **`GetByIdAsync` includes `RefinementAnswers`** — Added `.Include(g => g.RefinementAnswers)` because the owned collection lives in a separate table (`GoalRefinementAnswers`) and is not auto-loaded by EF Core without an explicit Include.
