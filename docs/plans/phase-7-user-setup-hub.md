# Phase 7: User Setup Hub Evolution & Route Corrections

**Status:** COMPLETED  
**Requirements:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` §P7  
**Source spec:** `docs/requirements/Phase-7-requirements.md`

---

## Objectives

1. Rename the onboarding flow from `SetupPage/SetupViewModel` to `CreateGoalPage/CreateGoalViewModel` and update its route to `"create-goal"`.
2. Fix the returning-user bug: on app start, if active Goals exist, enable the tab bar and navigate to the Goals page.
3. Add a `GetActiveGoalCountQuery` to drive both the startup check and the UserSetupHub warning state.
4. Implement a `FactoryResetCommand` backed by `IFactoryResetService` that atomically wipes all domain data and resets the `OnboardingSession`.
5. Build the permanent `UserSetupHub` page (route `"user-setup"`) accessible from the HUD profile icon (only when onboarding is complete).

---

## Acceptance Criteria

- [x] `dotnet build LifeGrid.slnx` → 0 errors, 0 warnings.
- [x] `dotnet test` → all 137 existing tests pass + all new Phase 7 tests pass.
- [x] App restart with existing goal: tab bar is enabled, Goals page opens automatically.
- [x] HUD profile icon during onboarding (no goal yet): tap is a no-op.
- [x] HUD profile icon after onboarding: navigates to `UserSetupPage`.
- [x] Warning text in UserSetupHub is visible (Error color) only when `HasActiveGoals = true`.
- [x] "Edit Active Goals" navigates to Goals list.
- [x] "Reset Goals" wipes all data, disables tabs, pushes CreateGoalPage.
- [x] "Detect Hidden Vices" is rendered but does nothing.
- [x] Full pipeline works again after Reset → new goal creation.

---

## Phase A — Domain Layer

### A1 — `OnboardingSession.Reset()`

**File:** `src/LifeGrid.Domain/Onboarding/OnboardingSession.cs`

Add a new public `Reset()` method:

```csharp
public void Reset()
{
    UserId                  = null;
    CurrentStep             = OnboardingStep.Unstarted;
    IsComplete              = false;
    RawGoalDraft            = null;
    ValidatedGoalJson       = null;
    RefinementQuestionsJson = null;
    RefinementAnswersJson   = null;
    LastActiveTimestamp     = DateTime.UtcNow;
}
```

### A2 — Domain Tests

**File:** `tests/LifeGrid.Domain.Tests/Onboarding/OnboardingSessionFactoryResetTests.cs` (new)

5 tests covering `Reset()`:
- `Reset_SetsCurrentStepToUnstarted`
- `Reset_ClearsIsComplete`
- `Reset_ClearsUserId`
- `Reset_ClearsAllStagingFields` (all JSON + RawGoalDraft null)
- `Reset_UpdatesLastActiveTimestamp`

Helper: `SessionAtHabitsGenerated()` — creates a fully-progressed session by calling all advance methods in sequence.

---

## Phase B — Application Layer

### B1 — Extend `IGoalRepository`

**File:** `src/LifeGrid.Application/Goal/IGoalRepository.cs`

Add:
```csharp
Task<int> GetActiveCountAsync(Guid userId, CancellationToken ct = default);
```

### B2 — `GetActiveGoalCountQuery`

**File:** `src/LifeGrid.Application/Goal/GetActiveGoalCountQuery.cs` (new)

```csharp
public record GetActiveGoalCountQuery : IRequest<Result<int>>;

public sealed class GetActiveGoalCountQueryHandler(
    IUserProfileRepository userProfileRepository,
    IGoalRepository        goalRepository)
    : IRequestHandler<GetActiveGoalCountQuery, Result<int>>
```

Logic:
- Load profile via `IUserProfileRepository.GetSingleAsync()`.
- If `null`, return `Result<int>.Success(0)`.
- Else return `Result<int>.Success(await goalRepository.GetActiveCountAsync(profile.UserId, ct))`.

### B3 — `IFactoryResetService`

**File:** `src/LifeGrid.Application/Common/IFactoryResetService.cs` (new)

```csharp
public interface IFactoryResetService
{
    Task ResetAsync(CancellationToken ct = default);
}
```

### B4 — `FactoryResetCommand`

**File:** `src/LifeGrid.Application/UserSetup/Commands/FactoryResetCommand.cs` (new)

```csharp
public record FactoryResetCommand : IRequest<Result>;

public sealed class FactoryResetCommandHandler(IFactoryResetService factoryResetService)
    : IRequestHandler<FactoryResetCommand, Result>
{
    public async Task<Result> Handle(FactoryResetCommand request, CancellationToken cancellationToken)
    {
        await factoryResetService.ResetAsync(cancellationToken);
        return Result.Success();
    }
}
```

### B5 — Application Tests

**File:** `tests/LifeGrid.Application.Tests/Goal/GetActiveGoalCountQueryTests.cs` (new)

3 tests:
- `NoProfile_ReturnsZero` — `IUserProfileRepository` returns null
- `NoActiveGoals_ReturnsZero` — repo returns 0
- `HasActiveGoals_ReturnsCount` — repo returns 2

**File:** `tests/LifeGrid.Application.Tests/UserSetup/FactoryResetCommandTests.cs` (new)

2 tests:
- `DelegatesTo_IFactoryResetService` — mock called once
- `ReturnsSuccess`

---

## Phase C — Infrastructure Layer

### C1 — `GoalRepository.GetActiveCountAsync`

**File:** `src/LifeGrid.Infrastructure/Data/Repositories/GoalRepository.cs`

Add implementation:
```csharp
public Task<int> GetActiveCountAsync(Guid userId, CancellationToken ct = default)
    => db.Goals
         .CountAsync(g => g.UserId == userId && g.Status == GoalStatus.Active, ct);
```

### C2 — `FactoryResetService`

**File:** `src/LifeGrid.Infrastructure/Data/Services/FactoryResetService.cs` (new)

Implements `IFactoryResetService`. Deletes domain data via `ExecuteSqlRawAsync` in FK-safe order, then resets the `OnboardingSession`:

```
DELETE order:
  1. Habits
  2. WeekGoals
  3. Weeks
  4. GoalRefinementAnswers
  5. GoalLinkedBadHabits
  6. Goals
  7. UserEconomy
  8. UserActiveStates
  9. UserBadges
 10. UserProfiles

Then: load active session → session.Reset() → UpsertAsync(session)
```

Registered as `Transient`.

### C3 — DI Registration

**File:** `src/LifeGrid.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`

Add:
```csharp
services.AddTransient<IFactoryResetService, FactoryResetService>();
```

### C4 — Infrastructure Integration Tests

**File:** `tests/LifeGrid.Infrastructure.Tests/Data/FactoryResetServiceTests.cs` (new)

Pattern: `SqliteConnection("Data Source=:memory:")` with `db.Database.Migrate()`.

Seed helper: creates `UserProfile`, `Goal`, `Week`, `WeekGoal`, `Habit`, and a completed `OnboardingSession`.

2 tests:
- `WithAllTablesPopulated_LeavesZeroRowsInAllDomainTables` — asserts counts for Habits, WeekGoals, Weeks, Goals, UserProfiles all = 0
- `ResetsOnboardingSessionToUnstarted` — asserts `CurrentStep = Unstarted`, `IsComplete = false`, `UserId = null`, all JSON fields null

---

## Phase D — Route Renaming

### D1 — Rename `SetupPage` → `CreateGoalPage`

**File renames:**
- `src/LifeGrid.Presentation/Pages/SetupPage.xaml` → `CreateGoalPage.xaml`
- `src/LifeGrid.Presentation/Pages/SetupPage.xaml.cs` → `CreateGoalPage.xaml.cs`

**Changes inside XAML:**
- `x:Class="LifeGrid.Presentation.Pages.CreateGoalPage"`
- `Title="Create Goal"`

**Changes inside `.xaml.cs`:**
- Class renamed to `CreateGoalPage`; namespace unchanged

### D2 — Rename `SetupViewModel` → `CreateGoalViewModel`

**File rename:**
- `src/LifeGrid.Presentation/ViewModels/SetupViewModel.cs` → `CreateGoalViewModel.cs`

**Changes inside file:**
- Class renamed to `CreateGoalViewModel`; no logic changes

### D3 — Update `AppShell.xaml.cs`

**File:** `src/LifeGrid.Presentation/AppShell.xaml.cs`

- Change: `Routing.RegisterRoute("setup", typeof(SetupPage))` → `Routing.RegisterRoute("create-goal", typeof(CreateGoalPage))`
- Add: `Routing.RegisterRoute("user-setup", typeof(UserSetupPage))`

### D4 — Update `MauiProgram.cs`

**File:** `src/LifeGrid.Presentation/MauiProgram.cs`

- Replace: `services.AddTransient<SetupViewModel>()` → `services.AddTransient<CreateGoalViewModel>()`
- Replace: `services.AddTransient<SetupPage>()` → `services.AddTransient<CreateGoalPage>()`
- Add: `services.AddTransient<UserSetupViewModel>()`
- Add: `services.AddTransient<UserSetupPage>()`

### D5 — Update `App.xaml.cs`

**File:** `src/LifeGrid.Presentation/App.xaml.cs`

- Add `AppShellViewModel` to constructor parameters.
- Replace `OnStart()` body (see Phase E below).

---

## Phase E — Startup Goal Detection Fix

### E1 — Updated `App.OnStart()`

**File:** `src/LifeGrid.Presentation/App.xaml.cs`

New `OnStart()` logic:

```csharp
protected override async void OnStart()
{
    base.OnStart();
    await _credentialSync.SyncAsync();
    await _mediator.Send(new GetOrCreateUserProfileQuery());

    var countResult = await _mediator.Send(new GetActiveGoalCountQuery());
    if (countResult.IsSuccess && countResult.Value > 0)
    {
        _appShellViewModel.SetOnboardingComplete();
        await Shell.Current.GoToAsync("//goals");
        return;
    }

    var sessionResult = await _mediator.Send(new GetOrCreateOnboardingSessionQuery());
    if (sessionResult.IsSuccess && !sessionResult.Value!.IsComplete)
        await Shell.Current.GoToAsync("create-goal");
}
```

Constructor signature becomes:
```csharp
public App(IServiceProvider services, IMediator mediator,
           IApiCredentialSyncService credentialSync, AppShellViewModel appShellViewModel)
```

---

## Phase F — HUD Profile Icon Update

### F1 — `HudView.xaml.cs`

**File:** `src/LifeGrid.Presentation/Controls/HudView.xaml.cs`

```csharp
private async void OnProfileTapped(object? sender, TappedEventArgs e)
{
    if (BindingContext is not AppShellViewModel { IsOnboardingComplete: true })
        return;
    await Shell.Current.GoToAsync("user-setup");
}
```

No XAML changes needed.

---

## Phase G — User Setup Hub

### G1 — `UserSetupViewModel`

**File:** `src/LifeGrid.Presentation/ViewModels/UserSetupViewModel.cs` (new)

```csharp
public partial class UserSetupViewModel(IMediator mediator, AppShellViewModel appShellViewModel)
    : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWarning))]
    private bool _hasActiveGoals = false;

    public bool ShowWarning => HasActiveGoals;

    public async Task LoadAsync()
    {
        var result = await mediator.Send(new GetActiveGoalCountQuery());
        if (result.IsSuccess)
            HasActiveGoals = result.Value > 0;
    }

    [RelayCommand]
    private async Task EditActiveGoalsAsync()
        => await Shell.Current.GoToAsync("//goals");

    [RelayCommand]
    private async Task ResetGoalsAsync()
    {
        await mediator.Send(new FactoryResetCommand());
        appShellViewModel.IsOnboardingComplete = false;
        await Shell.Current.GoToAsync("create-goal");
    }

    [RelayCommand]
    private void DetectHiddenVices() { }
}
```

### G2 — `UserSetupPage.xaml`

**File:** `src/LifeGrid.Presentation/Pages/UserSetupPage.xaml` (new)

Layout: `ContentPage` wrapping `ScrollView` > `VerticalStackLayout` (Padding=16, Spacing=24).

Components:
1. **Goal Management Block** (`VerticalStackLayout`, Spacing=12):
   - `Label` Text="User Setup" with `HeadlineStyle`
   - `Button` Text="Edit Active Goals" (Secondary style) → `EditActiveGoalsCommand`
   - `Button` Text="Reset Goals" (`TextColor=Error`, `BorderColor=Error`, transparent background) → `ResetGoalsCommand`
2. **Conditional Warning** (`Label`):
   - `Text="Warning: Resetting your goals will permanently wipe all current goals, Shield Points, XP, and restart your game from scratch."`
   - `TextColor="{StaticResource Error}"`
   - `IsVisible="{Binding ShowWarning}"`
3. **Diagnostics** (`VerticalStackLayout`):
   - `Button` Text="Detect Hidden Vices" → `DetectHiddenVicesCommand`

All styling tokens sourced from `Resources/Styles/Colors.xaml` — no hardcoded hex values.

**File:** `src/LifeGrid.Presentation/Pages/UserSetupPage.xaml.cs` (new)

Standard code-behind; `OnAppearing` calls `await ViewModel.LoadAsync()`.

---

## Phase H — ViewModel Tests

### H1 — `UserSetupViewModelTests`

**File:** `tests/LifeGrid.Application.Tests/UserSetup/UserSetupViewModelTests.cs` (new)

**Note:** Placed in `LifeGrid.Application.Tests` since there is no separate Presentation test project. The ViewModel references `IMediator` (mocked) and `AppShellViewModel` (real instance, no mocking needed).

2 tests:
- `LoadAsync_ZeroGoals_ShowWarningIsFalse` — mock `IMediator` returns count 0; assert `ShowWarning == false`
- `LoadAsync_HasGoals_ShowWarningIsTrue` — mock `IMediator` returns count 1; assert `ShowWarning == true`

---

## Execution Order

```
A (Domain) → A2 (tests)
B1 (IGoalRepository) → B2 (query) → B3 (interface) → B4 (command) → B5 (tests)
C1 (repo impl) → C2 (service impl) → C3 (DI) → C4 (integration tests)
D (renames: D1 → D2 → D3 → D4 → D5 in sequence)
E (startup fix — depends on D5 and B2)
F (HUD update — no dependencies)
G (UserSetupView — depends on B2, B4, E)
H (VM tests — depends on G1)
Build + Full test run
```

---

## Files Changed / Created Summary

| Action | File |
|---|---|
| Edit | `src/LifeGrid.Domain/Onboarding/OnboardingSession.cs` |
| New | `tests/LifeGrid.Domain.Tests/Onboarding/OnboardingSessionFactoryResetTests.cs` |
| Edit | `src/LifeGrid.Application/Goal/IGoalRepository.cs` |
| New | `src/LifeGrid.Application/Goal/GetActiveGoalCountQuery.cs` |
| New | `src/LifeGrid.Application/Common/IFactoryResetService.cs` |
| New | `src/LifeGrid.Application/UserSetup/Commands/FactoryResetCommand.cs` |
| New | `tests/LifeGrid.Application.Tests/Goal/GetActiveGoalCountQueryTests.cs` |
| New | `tests/LifeGrid.Application.Tests/UserSetup/FactoryResetCommandTests.cs` |
| New | `tests/LifeGrid.Application.Tests/UserSetup/UserSetupViewModelTests.cs` |
| Edit | `src/LifeGrid.Infrastructure/Data/Repositories/GoalRepository.cs` |
| New | `src/LifeGrid.Infrastructure/Data/Services/FactoryResetService.cs` |
| Edit | `src/LifeGrid.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` |
| New | `tests/LifeGrid.Infrastructure.Tests/Data/FactoryResetServiceTests.cs` |
| Rename+Edit | `src/LifeGrid.Presentation/Pages/SetupPage.xaml` → `CreateGoalPage.xaml` |
| Rename+Edit | `src/LifeGrid.Presentation/Pages/SetupPage.xaml.cs` → `CreateGoalPage.xaml.cs` |
| Rename+Edit | `src/LifeGrid.Presentation/ViewModels/SetupViewModel.cs` → `CreateGoalViewModel.cs` |
| Edit | `src/LifeGrid.Presentation/AppShell.xaml.cs` |
| Edit | `src/LifeGrid.Presentation/MauiProgram.cs` |
| Edit | `src/LifeGrid.Presentation/App.xaml.cs` |
| Edit | `src/LifeGrid.Presentation/Controls/HudView.xaml.cs` |
| New | `src/LifeGrid.Presentation/ViewModels/UserSetupViewModel.cs` |
| New | `src/LifeGrid.Presentation/Pages/UserSetupPage.xaml` |
| New | `src/LifeGrid.Presentation/Pages/UserSetupPage.xaml.cs` |
