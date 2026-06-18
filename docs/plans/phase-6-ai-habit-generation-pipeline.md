# Phase 6 — AI Habit Generation Pipeline & Onboarding Finalization
## Implementation Plan

**Status:** COMPLETED  
**Requirements:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` §P6.1–P6.19  
**Source spec:** `docs/requirements/Phase-6-requirements.md`

---

## Pre-Flight Analysis (LEAD_ARCHITECT)

### Bounded Context Impact
- **GoalManagement** (existing) — extended with `Week`, `WeekGoal`, `Habit` sub-entities that attach to an existing `Goal` aggregate. `GoalId` is an ID-only reference from `WeekGoal` (no navigation crossing the context boundary).
- **Onboarding** (existing) — `OnboardingSession` gains one terminal step (`Step6_HabitsGenerated`) that sets `IsComplete = true`, unblocking the global navigation.
- No new bounded context required.

### Critical Architectural Decisions
| Decision | Rationale |
|---|---|
| Auto-trigger habit generation (no extra button) | Seamless pipeline; matches functional spec §1.5 |
| Infeasibility as `Result.Success(Infeasible)` not `Result.Failure` | Infeasibility is a valid domain outcome, not a technical failure; mirrors `GeminiValidationResult.Invalid` pattern |
| `GetActiveSessionAsync` filters `!IsComplete` | Required for "Add Goal" flow — completed session must not block a new session from being returned |
| `CompletedValuesLog` deferred | Not needed in Phase 6; avoids over-engineering the schema before the execution phase |
| `GoalId` in `WeekGoal` is ID-only reference | Clean Architecture aggregate boundary — `Habit` domain stays decoupled from `Goal` domain |

### MediatR Command Chain (runtime flow)
```
ConfirmAndInitialize button tap
  → FinalizeGoalCommand          (Phase 5 — persists Goal)
  → GenerateHabitsCommand        (Phase 6 — AI pipeline + persist schedule)
      ├── IGeminiHabitGenerationService.GenerateScheduleAsync
      │     ├── Gemini call 1: prompt2.1 (blueprint parameters)
      │     └── Gemini call 2: prompt2.2 (week-by-week schedule)
      └── IHabitScheduleRepository.SaveScheduleAsync (atomic transaction)
  → session.AdvanceToHabitsGenerated() → IsComplete = true
  → AppShellViewModel.SetOnboardingComplete()
  → Shell.GoToAsync("//goals")
```

---

## Persona Assignment

| Phase | Persona |
|---|---|
| A – Domain | `SENIOR_NET_DEVELOPER` |
| B – Application DTOs & interfaces | `LEAD_ARCHITECT` |
| C – Application commands | `LEAD_ARCHITECT` + `GAMIFICATION_SPECIALIST` (command guard invariants) |
| D – Infrastructure AI | `AI_INTEGRATION_ENGINEER` |
| E – Infrastructure DB | `SENIOR_NET_DEVELOPER` |
| F – Presentation | `MAUI_UX_ENGINEER` |
| G – Tests | `TDD_SPECIALIST` |

---

## Phase A — Domain Layer

### A1: New Enums
**Files to create** (all in `src/LifeGrid.Domain/Habit/`):
- `WeekStatus.cs`: `Active | Hibernated | Frozen`
- `PenaltyState.cs`: `Clean | Level1Warning | ProbationWeek2 | ReckoningWeek3`
- `HabitType.cs`: `Planned | MomentBurst | Flash`

All are `public enum`.

### A2: `Week` Entity
**File:** `src/LifeGrid.Domain/Habit/Week.cs`
```csharp
public sealed class Week
{
    private readonly List<WeekGoal> _weekGoals = new();
    private Week() { }

    public static Week Create(int weekNumber, DateTime startDate) => new()
    {
        WeekId                = Guid.NewGuid(),
        WeekNumber            = weekNumber,
        StartDate             = startDate,
        Status                = WeekStatus.Active,
        TotalWeeklySpEarned   = 0
    };

    public Guid       WeekId              { get; private set; }
    public int        WeekNumber          { get; private set; }
    public DateTime   StartDate           { get; private set; }
    public WeekStatus Status              { get; private set; }
    public int        TotalWeeklySpEarned { get; private set; }
    public IReadOnlyCollection<WeekGoal> WeekGoals => _weekGoals.AsReadOnly();
}
```

### A3: `WeekGoal` Entity
**File:** `src/LifeGrid.Domain/Habit/WeekGoal.cs`
```csharp
public sealed class WeekGoal
{
    private readonly List<Habit> _habits = new();
    private WeekGoal() { }

    public static WeekGoal Create(Guid weekId, Guid goalId) => new()
    {
        WeekGoalId          = Guid.NewGuid(),
        WeekId              = weekId,
        GoalId              = goalId,
        PenaltyState        = PenaltyState.Clean,
        GoalWeeklyGp        = 0.0,
        GoalWeeklyXpEarned  = 0
    };

    public Guid         WeekGoalId         { get; private set; }
    public Guid         WeekId             { get; private set; }
    public Guid         GoalId             { get; private set; }  // ID-only cross-aggregate ref
    public PenaltyState PenaltyState       { get; private set; }
    public double       GoalWeeklyGp       { get; private set; }
    public int          GoalWeeklyXpEarned { get; private set; }
    public IReadOnlyCollection<Habit> Habits => _habits.AsReadOnly();
}
```

### A4: `Habit` Entity
**File:** `src/LifeGrid.Domain/Habit/Habit.cs`
```csharp
public sealed class Habit
{
    private Habit() { }

    public static Habit Create(
        Guid weekGoalId, string habitName, string habitDescription,
        double targetValue, string measurementUnit, DateTime deadlineDateTime) => new()
    {
        HabitId          = Guid.NewGuid(),
        WeekGoalId       = weekGoalId,
        HabitType        = HabitType.Planned,
        HabitName        = habitName,
        HabitDescription = habitDescription,
        TargetValue      = targetValue,
        MeasurementUnit  = measurementUnit,
        DeadlineDateTime = deadlineDateTime
    };

    public Guid      HabitId          { get; private set; }
    public Guid      WeekGoalId       { get; private set; }
    public HabitType HabitType        { get; private set; }
    public string    HabitName        { get; private set; } = string.Empty;
    public string    HabitDescription { get; private set; } = string.Empty;
    public double    TargetValue      { get; private set; }
    public string    MeasurementUnit  { get; private set; } = string.Empty;
    public DateTime  DeadlineDateTime { get; private set; }
}
```

### A5: Update `OnboardingStep` Enum
**File:** `src/LifeGrid.Domain/Onboarding/OnboardingStep.cs`  
Add `Step6_HabitsGenerated` after `Step1_ExecutionVerified`.

### A6: Update `OnboardingSession`
**File:** `src/LifeGrid.Domain/Onboarding/OnboardingSession.cs`  
Add new domain method:
```csharp
public void AdvanceToHabitsGenerated()
{
    IsComplete          = true;
    CurrentStep         = OnboardingStep.Step6_HabitsGenerated;
    LastActiveTimestamp = DateTime.UtcNow;
}
```
`IsComplete` must change from `{ get; private set; }` to allow the `private set` assignment within the same class. (It already has `private set` — no change needed there.)

---

## Phase B — Application Layer: DTOs & Interfaces

### B1: Habit DTOs
**Files to create** in `src/LifeGrid.Application/Habit/`:
- `HabitScheduleItemDto.cs`: `record HabitScheduleItemDto(string Description, double Value, string Unit)`
- `WeekScheduleDto.cs`: `record WeekScheduleDto(int WeekNumber, DateTime StartDate, IReadOnlyList<HabitScheduleItemDto> Habits)`

### B2: `HabitSchedulingResult` (service-layer discriminated union)
**File:** `src/LifeGrid.Application/Habit/HabitSchedulingResult.cs`
```csharp
public abstract record HabitSchedulingResult
{
    public sealed record Feasible(IReadOnlyList<WeekScheduleDto> Schedule) : HabitSchedulingResult;
    public sealed record Infeasible(
        string  RecalibrationReason,
        string? SuggestedDeadline,
        string? SuggestedAlternativeScope) : HabitSchedulingResult;
}
```

### B3: `HabitGenerationOutcome` (command-layer discriminated union)
**File:** `src/LifeGrid.Application/Habit/HabitGenerationOutcome.cs`
```csharp
public abstract record HabitGenerationOutcome
{
    public sealed record Complete : HabitGenerationOutcome;
    public sealed record Infeasible(
        string  RecalibrationReason,
        string? SuggestedDeadline,
        string? SuggestedAlternativeScope) : HabitGenerationOutcome;
}
```

### B4: `IGeminiHabitGenerationService`
**File:** `src/LifeGrid.Application/Habit/IGeminiHabitGenerationService.cs`
```csharp
public interface IGeminiHabitGenerationService
{
    Task<Result<HabitSchedulingResult>> GenerateScheduleAsync(
        string goalAsStated,
        string deadlineAsStated,
        string baselineAnswersJson,
        CancellationToken ct = default);
}
```

### B5: `IHabitScheduleRepository`
**File:** `src/LifeGrid.Application/Habit/IHabitScheduleRepository.cs`
```csharp
public interface IHabitScheduleRepository
{
    Task SaveScheduleAsync(
        Guid goalId,
        IReadOnlyList<WeekScheduleDto> schedule,
        CancellationToken ct = default);
}
```

### B6: Extend `IGoalRepository`
**File:** `src/LifeGrid.Application/Goal/IGoalRepository.cs` (existing)  
Add:
```csharp
Task<GoalAggregate?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
Task<IReadOnlyList<GoalAggregate>> GetAllByUserIdAsync(Guid userId, CancellationToken ct = default);
```

### B7: `GoalSummaryDto`
**File:** `src/LifeGrid.Application/Goal/GoalSummaryDto.cs`
```csharp
public record GoalSummaryDto(
    Guid     GoalId,
    string   Description,
    string   AmbientTag,
    string   Duration,
    DateTime DeadlineDate,
    string   Status);
```

### B8: `GetGoalsQuery`
**File:** `src/LifeGrid.Application/Goal/GetGoalsQuery.cs`
- `GetGoalsQuery : IRequest<Result<IReadOnlyList<GoalSummaryDto>>>`
- Handler: `GetSingleAsync()` → `GetAllByUserIdAsync(userId)` → map to `GoalSummaryDto` list.

---

## Phase C — Application Layer: Commands

### C1: `GenerateHabitsCommand`
**File:** `src/LifeGrid.Application/Habit/Commands/GenerateHabitsCommand.cs`

```
GenerateHabitsCommand : IRequest<Result<HabitGenerationOutcome>>
```

Handler constructor parameters: `IOnboardingRepository`, `IUserProfileRepository`, `IGoalRepository`, `IGeminiHabitGenerationService`, `IHabitScheduleRepository`

Handler steps (map to P6.7):
1. Load session; guard `Step1_ExecutionVerified`
2. Load user profile; guard non-null
3. Load goal via `GetByUserIdAsync`; guard non-null
4. Build `baselineAnswersJson` — serialize `goal.RefinementAnswers` as `[{"question":"...","answer":"..."},...]`
5. Call `GenerateScheduleAsync`; on failure → `Result.Failure`
6. Switch on `HabitSchedulingResult`:
   - `Infeasible` → `Result.Success(HabitGenerationOutcome.Infeasible(...))`
   - `Feasible` → save schedule → advance session → `Result.Success(HabitGenerationOutcome.Complete)`

### C2: `StartNewGoalSessionCommand`
**File:** `src/LifeGrid.Application/Onboarding/Commands/StartNewGoalSessionCommand.cs`

```csharp
public record StartNewGoalSessionCommand : IRequest<Result>;

// Handler: creates OnboardingSession.Create(), persists via IOnboardingRepository.UpsertAsync()
```

---

## Phase D — Infrastructure: AI Service

### D1: Embed Prompt Files
- **Copy** `docs/specs/assets/prompts/prompt2.1.txt` → `src/LifeGrid.Infrastructure/AI/Prompts/prompt2.1.txt`
- **Copy** `docs/specs/assets/prompts/prompt2.2.txt` → `src/LifeGrid.Infrastructure/AI/Prompts/prompt2.2.txt`
- **Register** both in `LifeGrid.Infrastructure.csproj`:
  ```xml
  <EmbeddedResource Include="AI\Prompts\prompt2.1.txt" />
  <EmbeddedResource Include="AI\Prompts\prompt2.2.txt" />
  ```

### D2: `GeminiHabitGenerationService`
**File:** `src/LifeGrid.Infrastructure/AI/GeminiHabitGenerationService.cs`

Key implementation notes:
- Load prompts via `Assembly.GetManifestResourceStream("LifeGrid.Infrastructure.AI.Prompts.prompt2.1.txt")` (same pattern as `GeminiGoalValidationService`)
- Prompt2.1 preprocessing: `$"[Current date: {DateTime.UtcNow:MMMM d, yyyy}]\n\n"` + substitutions
- Substitution keys: `${USER_GOAL}`, `${USER_DEADLINE}`, `${USER_BASELINE_ANSWERS_JSON}`
- Call 1 JSON parsing: check `isFeasible` (bool); if false, read `recalibration_reason`, `recommended_recalibration.suggested_deadline`, `recommended_recalibration.suggested_alternative_scope`
- **Pass raw Call 1 response string to Call 2** — do NOT re-serialize; inject the string directly as `${COACH_SPECIALIST_PARAMETERS_JSON}`
- Call 2 JSON parsing: deserialize `weeks[]` array; each week: `week_number` (int), `start_date` (string → `DateTime.Parse`), `habits[]` with `description`, `measurement.value`, `measurement.unit`
- `StripCodeFences()` helper (copy pattern from `GeminiGoalValidationService`) before parsing both responses
- Error handling: `catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)` → `Result.Failure(ex.Message)`; general `catch (Exception ex)` → `Result.Failure($"...")`

---

## Phase E — Infrastructure: Database

### E1: EF Core Configurations
**Files to create** in `src/LifeGrid.Infrastructure/Data/EntityConfigurations/`:

**`WeekConfiguration.cs`**
```csharp
builder.ToTable("Weeks");
builder.HasKey(w => w.WeekId);
builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(30);
builder.HasMany(w => w.WeekGoals)
       .WithOne()
       .HasForeignKey(wg => wg.WeekId)
       .OnDelete(DeleteBehavior.Cascade);
```

**`WeekGoalConfiguration.cs`**
```csharp
builder.ToTable("WeekGoals");
builder.HasKey(wg => wg.WeekGoalId);
builder.Property(wg => wg.PenaltyState).HasConversion<string>().HasMaxLength(30);
builder.Property(wg => wg.GoalId);  // no FK constraint to Goals to avoid cross-context cascade
builder.HasMany(wg => wg.Habits)
       .WithOne()
       .HasForeignKey(h => h.WeekGoalId)
       .OnDelete(DeleteBehavior.Cascade);
```

**`HabitConfiguration.cs`**
```csharp
builder.ToTable("Habits");
builder.HasKey(h => h.HabitId);
builder.Property(h => h.HabitType).HasConversion<string>().HasMaxLength(30);
builder.Property(h => h.HabitName).HasMaxLength(500);
builder.Property(h => h.HabitDescription).HasMaxLength(2000);
builder.Property(h => h.MeasurementUnit).HasMaxLength(100);
```

### E2: Update `LifeGridDbContext`
**File:** `src/LifeGrid.Infrastructure/Data/LifeGridDbContext.cs`  
Add:
```csharp
public DbSet<Week>     Weeks     => Set<Week>();
public DbSet<WeekGoal> WeekGoals => Set<WeekGoal>();
public DbSet<Habit>    Habits    => Set<Habit>();
```

### E3: Fix `OnboardingRepository.GetActiveSessionAsync`
**File:** `src/LifeGrid.Infrastructure/Data/Repositories/OnboardingRepository.cs`  
Change:
```csharp
// before:
public Task<OnboardingSession?> GetActiveSessionAsync(CancellationToken ct = default)
    => db.OnboardingSessions.FirstOrDefaultAsync(ct);

// after:
public Task<OnboardingSession?> GetActiveSessionAsync(CancellationToken ct = default)
    => db.OnboardingSessions.FirstOrDefaultAsync(s => !s.IsComplete, ct);
```

### E4: Extend `GoalRepository`
**File:** `src/LifeGrid.Infrastructure/Data/Repositories/GoalRepository.cs`  
Add implementations of `GetByUserIdAsync` and `GetAllByUserIdAsync` using `db.Goals.Where(g => g.UserId == userId)` with EF Core query methods.

### E5: `HabitScheduleRepository`
**File:** `src/LifeGrid.Infrastructure/Data/Repositories/HabitScheduleRepository.cs`

Atomic transaction pattern:
```csharp
await using var tx = await _db.Database.BeginTransactionAsync(ct);
try
{
    // create and add all Week/WeekGoal/Habit entities
    await _db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);
}
catch
{
    await tx.RollbackAsync(ct);
    throw;
}
```

### E6: Update DI Registration
**File:** `src/LifeGrid.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs`  
Add:
```csharp
services.AddTransient<IGeminiHabitGenerationService, GeminiHabitGenerationService>();
services.AddScoped<IHabitScheduleRepository, HabitScheduleRepository>();
```

### E7: Generate Migration
```
dotnet ef migrations add AddHabitScheduleSchema --project src/LifeGrid.Infrastructure --startup-project src/LifeGrid.Presentation
```
Verify migration creates: `Weeks`, `WeekGoals`, `Habits` tables. Existing tables must be untouched.

---

## Phase F — Presentation Layer

### F1: Update `SetupViewModel`
**File:** `src/LifeGrid.Presentation/ViewModels/SetupViewModel.cs`

Changes:
- Add `AppShellViewModel _appShellViewModel` constructor parameter
- Add `[ObservableProperty] bool _isGeneratingHabits`
- Add `[ObservableProperty] string _infeasibilityReason = string.Empty`
- Remove `[ObservableProperty] bool _isExecutionVerified` (no longer shown in UI; the state is session-level only)
- Modify `ConfirmAndInitializeAsync`:
  ```csharp
  var finalizeResult = await mediator.Send(new FinalizeGoalCommand(userAnswers));
  if (!finalizeResult.IsSuccess) { /* show error */ return; }

  IsRefinementActive = false;
  IsGeneratingHabits = true;

  var habitResult = await mediator.Send(new GenerateHabitsCommand());

  IsGeneratingHabits = false;

  switch (habitResult)
  {
      case { IsSuccess: false }:
          ValidationError = habitResult.Error ?? "Habit generation failed.";
          break;
      case { Value: HabitGenerationOutcome.Infeasible infeasible }:
          InfeasibilityReason = infeasible.RecalibrationReason
              + (infeasible.SuggestedDeadline is not null
                  ? $"\n\nSuggested deadline: {infeasible.SuggestedDeadline}"
                  : string.Empty);
          break;
      case { Value: HabitGenerationOutcome.Complete }:
          _appShellViewModel.SetOnboardingComplete();
          await Shell.Current.GoToAsync("//goals");
          break;
  }
  ```
- Add `[RelayCommand] void ReviseGoal()`: resets `InfeasibilityReason = string.Empty`, `GoalDraft = string.Empty`, triggers State A

### F2: Update `SetupPage.xaml`
**File:** `src/LifeGrid.Presentation/Pages/SetupPage.xaml`

Remove the Phase 5 State E panel ("Goal Refined and Stored...").

Add two new panels inside the main VerticalStackLayout, after the refinement questions panel:

**State E — Generating (after `IsRefinementActive` panel):**
```xml
<VerticalStackLayout
    IsVisible="{Binding IsGeneratingHabits}"
    Spacing="12"
    HorizontalOptions="Center">
    <ActivityIndicator IsRunning="{Binding IsGeneratingHabits}" Color="{StaticResource Primary}" HorizontalOptions="Center" />
    <Label Text="Crafting your personalized plan..." HorizontalOptions="Center" HorizontalTextAlignment="Center" />
</VerticalStackLayout>
```

**State F — Infeasible:**
```xml
<VerticalStackLayout
    IsVisible="{Binding InfeasibilityReason, Converter={StaticResource StringNotEmpty}}"
    Spacing="16">
    <Label Text="{Binding InfeasibilityReason}" TextColor="{StaticResource Error}" />
    <Button Text="Revise Your Goal" Command="{Binding ReviseGoalCommand}" />
</VerticalStackLayout>
```

**Note:** `IsRefinementActive` already has an `InverseBool` toggle on the outer panel. The outer panel visibility condition needs updating to also hide when `IsGeneratingHabits = true` or `InfeasibilityReason` is non-empty. Simplest approach: the outer non-refinement panel uses:
```xml
IsVisible="{Binding IsRefinementActive, Converter={StaticResource InverseBool}}"
```
But we need to hide the goal entry form during generating/infeasible states. Restructure: add a top-level condition or nest the states more carefully during implementation.

### F3: Update `AppShellViewModel`
**File:** `src/LifeGrid.Presentation/ViewModels/AppShellViewModel.cs`  
Add:
```csharp
public void SetOnboardingComplete() => IsOnboardingComplete = true;
```

### F4: `GoalSummaryItem`
**File:** `src/LifeGrid.Presentation/ViewModels/GoalSummaryItem.cs`
```csharp
public record GoalSummaryItem(
    Guid     GoalId,
    string   Description,
    string   AmbientTag,
    string   Duration,
    DateTime DeadlineDate,
    string   Status);
```

### F5: `GoalsViewModel`
**File:** `src/LifeGrid.Presentation/ViewModels/GoalsViewModel.cs`
```csharp
public partial class GoalsViewModel(IMediator mediator) : ObservableObject
{
    public ObservableCollection<GoalSummaryItem> Goals { get; } = new();

    public async Task LoadAsync()
    {
        var result = await mediator.Send(new GetGoalsQuery());
        if (!result.IsSuccess) return;
        Goals.Clear();
        foreach (var dto in result.Value!)
            Goals.Add(new GoalSummaryItem(dto.GoalId, dto.Description, dto.AmbientTag, dto.Duration, dto.DeadlineDate, dto.Status));
    }

    [RelayCommand]
    private async Task AddGoalAsync()
    {
        await mediator.Send(new StartNewGoalSessionCommand());
        await Shell.Current.GoToAsync("setup");
    }
}
```

### F6: Update `GoalsPage.xaml` and `GoalsPage.xaml.cs`
**File:** `src/LifeGrid.Presentation/Pages/GoalsPage.xaml`

Replace the placeholder content in the `ScrollView` with:
```xml
<Grid RowDefinitions="*,Auto">
    <CollectionView Grid.Row="0" ItemsSource="{Binding Goals}">
        <CollectionView.EmptyView>
            <Label Text="No goals yet" HorizontalOptions="Center" VerticalOptions="Center" />
        </CollectionView.EmptyView>
        <CollectionView.ItemTemplate>
            <DataTemplate>
                <VerticalStackLayout Padding="16,8" Spacing="4">
                    <Label Text="{Binding Description}" Style="{StaticResource HeadlineStyle}" />
                    <Label Text="{Binding AmbientTag}" />
                    <Label Text="{Binding DeadlineDate, StringFormat='{0:yyyy-MM-dd}'}"
                           TextColor="{StaticResource Primary}" />
                </VerticalStackLayout>
            </DataTemplate>
        </CollectionView.ItemTemplate>
    </CollectionView>
    <Button Grid.Row="1" Text="Add Goal" Command="{Binding AddGoalCommand}" Margin="16,8" />
</Grid>
```

**File:** `src/LifeGrid.Presentation/Pages/GoalsPage.xaml.cs`  
Add `OnAppearing` override that calls `await _viewModel.LoadAsync()` (same pattern as SetupPage).

### F7: Wire DI in `MauiProgram.cs`
Add registrations:
```csharp
builder.Services.AddTransient<GoalsViewModel>();
builder.Services.AddTransient<GoalsPage>();
```

---

## Phase G — Tests

### G1: Domain Tests
**File:** `tests/LifeGrid.Domain.Tests/Habit/HabitEntityTests.cs`

> **Note:** Apply the type-alias pattern from P4.13 to avoid namespace collision:
> `using WeekEntity = LifeGrid.Domain.Habit.Week;`

Tests to write (6):
- `Week_Create_SetsStatusToActive`
- `Week_Create_SetsWeekNumberAndStartDate`
- `WeekGoal_Create_SetsDefaultsCorrectly` (PenaltyState=Clean, Gp=0.0, XpEarned=0)
- `Habit_Create_PreservesAllTargetFields`
- `OnboardingSession_AdvanceToHabitsGenerated_SetsIsCompleteTrue`
- `OnboardingSession_AdvanceToHabitsGenerated_SetsStep6`

### G2: Application Tests
**File:** `tests/LifeGrid.Application.Tests/Habit/GenerateHabitsCommandTests.cs`

NSubstitute mock: `IGeminiHabitGenerationService`, `IHabitScheduleRepository`, `IOnboardingRepository`, `IUserProfileRepository`, `IGoalRepository`

Tests to write (6):
- `Handle_HappyPath_AdvancesSessionToHabitsGenerated`
- `Handle_HappyPath_CallsSaveScheduleWithGoalId`
- `Handle_HappyPath_ReturnsCompleteOutcome`
- `Handle_TechnicalFailure_ReturnsFailureWithoutSaving`
- `Handle_Infeasible_ReturnsInfeasibleOutcomeWithoutSaving`
- `Handle_NoActiveSession_ReturnsFailure`

**File:** `tests/LifeGrid.Application.Tests/Onboarding/StartNewGoalSessionCommandTests.cs`

Tests to write (2):
- `Handle_CreatesNewSession`
- `Handle_PersistsSession`

### G3: Infrastructure AI Tests
**File:** `tests/LifeGrid.Infrastructure.Tests/AI/GeminiHabitGenerationServiceTests.cs`

Mock `IChatClient` to return controlled JSON for each call (use `CallCount` or `Received().GetResponseAsync()` with argument matchers).

Tests to write (6):
- `GenerateSchedule_HappyPath_ReturnsFeasibleWithCorrectWeekCount`
- `GenerateSchedule_Infeasible_ReturnsInfeasibleResult`
- `GenerateSchedule_ChainedCall_Call2PromptContainsCall1RawJson`
- `GenerateSchedule_Call1MalformedJson_ReturnsFailure`
- `GenerateSchedule_Call2MalformedJson_ReturnsFailure`
- `GenerateSchedule_RateLimit_ReturnsFailureWithWaitHint`

### G4: Infrastructure DB Integration Tests
**File:** `tests/LifeGrid.Infrastructure.Tests/Data/HabitScheduleRepositoryTests.cs`

Use real SQLite `:memory:` with `context.Database.Migrate()`.

Tests to write (2):
- `SaveSchedule_PersistsAllWeekGoalAndHabitRows`
- `SaveSchedule_RollbackOnException_LeavesZeroOrphanedRows` — inject a post-save exception by overriding `SaveChangesAsync` or using a partial mock; assert all three tables empty after the call

---

## Dependency & Execution Order

The phases are strictly sequential within their layer boundaries:

```
A (Domain) ──► B (App DTOs) ──► C (App Commands) ──► D (Infra AI)
                                                  ──► E (Infra DB)
                                                  ──► F (Presentation)
                                                  ──► G (Tests — written alongside each phase)
```

TDD mandate: write the test stubs for each phase **before** implementing the code, then make them pass.

---

## Acceptance Criteria Checklist

- [x] `dotnet build LifeGrid.slnx` → 0 errors, 0 warnings
- [x] `dotnet test` → all 87 existing tests pass
- [x] New domain tests pass (≥6)
- [x] New application tests pass (≥8)
- [x] New infrastructure AI tests pass (≥6)
- [x] New infrastructure DB integration tests pass (≥2)
- [x] After "Confirm & Initialize": spinner appears, then Goals tab opens with nav enabled
- [x] Goals page shows the new goal (description, ambient tag, deadline)
- [x] "Add Goal" runs full pipeline for a second goal
- [x] Infeasibility path: recalibration message shown; "Revise Your Goal" returns to entry form
- [x] Rollback test: zero orphaned rows after forced save failure
