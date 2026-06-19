# Phase 8: Global HUD Metric Wiring & Economy Data Binding

**Status:** COMPLETE  
**Requirements:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` §P8  
**Source spec:** `docs/requirements/Phase-8-requirements.md`

---

## Objectives

1. Implement `GetHudTelemetryQuery` — aggregates n1–n9 from `UserProfile`, the active `Week`, and its `WeekGoal` items.
2. Extend `IWeekRepository` with `GetActiveAsync()` to load the current week + WeekGoals in one query.
3. Create a dedicated `HudViewModel` (Singleton) with 9 bindable string properties and a `LoadAsync()` method.
4. Wire `HudView.BindingContext` to `HudViewModel`; fix `OnProfileTapped` to read `AppShellViewModel` from `Shell.Current`.
5. Rebuild `HudView.xaml` center column: 4 two-tone telemetry pairs (`ShareTechMono`; weekly = Primary, lifetime = OnSurface) + level circular badge.
6. Call `HudViewModel.LoadAsync()` from `App.OnStart()` after UserProfile creation.

---

## Acceptance Criteria

- [x] `dotnet build LifeGrid.slnx` → 0 errors, 0 warnings.
- [x] `dotnet test` → all 149 existing tests pass + all 4 new Phase 8 tests pass (153 total).
- [x] HUD center panel renders 4 pairs: GP, XP, SP, Shields in `ShareTechMono` font.
- [x] Weekly values (n3 GpWeekly, n5 XpWeekly, n7 SpWeekly, n8 ShieldsActive) use `Primary` text color.
- [x] Lifetime/capacity values (n2 GpLifetime, n4 XpLifetime, n6 SpCurrent, n9 ShieldsCap) use `OnSurface` text color.
- [x] Level badge renders as a circular `Primary`-bordered `Border` element adjacent to the profile icon.
- [x] HUD profile icon tap still navigates to `user-setup` when `IsOnboardingComplete = true`.
- [x] No new EF Core migration generated.

---

## Phase A — Application Layer

### A1 — `HudTelemetryDto`

**File:** `src/LifeGrid.Application/Hud/HudTelemetryDto.cs` (new)

```csharp
namespace LifeGrid.Application.Hud;

public record HudTelemetryDto(
    int    Level,
    double LifetimeGp,
    double WeeklyGp,
    int    LifetimeXp,
    int    WeeklyXp,
    int    CurrentSp,
    int    WeeklySpEarned,
    int    ActiveShields,
    int    ShieldCap);
```

### A2 — Extend `IWeekRepository`

**File:** `src/LifeGrid.Application/Week/IWeekRepository.cs`

Add:
```csharp
Task<WeekEntity?> GetActiveAsync(CancellationToken ct = default);
```

Contract: returns the first `Week` with `Status == WeekStatus.Active`, with `WeekGoals` navigation populated. Returns `null` if no active week exists.

### A3 — `GetHudTelemetryQuery`

**File:** `src/LifeGrid.Application/Hud/GetHudTelemetryQuery.cs` (new)

```csharp
public record GetHudTelemetryQuery : IRequest<Result<HudTelemetryDto>>;

public sealed class GetHudTelemetryQueryHandler(
    IUserProfileRepository userProfileRepository,
    IWeekRepository        weekRepository)
    : IRequestHandler<GetHudTelemetryQuery, Result<HudTelemetryDto>>
```

Handler logic:
1. `profile = await userProfileRepository.GetSingleAsync(ct)`.
2. If `profile is null` → return `Result<HudTelemetryDto>.Success(new HudTelemetryDto(0, 0.0, 0.0, 0, 0, 0, 0, 0, 2))`.
3. `week = await weekRepository.GetActiveAsync(ct)`.
4. If `week is null`:
   - Return DTO with `Level=profile.CurrentLevel`, `LifetimeGp=profile.Economy.LifetimeGpAverage`, `WeeklyGp=0.0`, `LifetimeXp=profile.Economy.LifetimeXp`, `WeeklyXp=0`, `CurrentSp=profile.Economy.CurrentSp`, `WeeklySpEarned=0`, `ActiveShields=profile.Economy.ShieldsAvailable`, `ShieldCap=profile.Economy.MaxShieldCap`.
5. Compute weekly metrics from `week.WeekGoals`:
   - `n3 = week.WeekGoals.Count > 0 ? week.WeekGoals.Average(wg => wg.GoalWeeklyGp) : 0.0`
   - `n5 = week.WeekGoals.Sum(wg => wg.GoalWeeklyXpEarned)`
   - `n7 = week.TotalWeeklySpEarned`
6. Return full DTO.

### A4 — Application Unit Tests

**File:** `tests/LifeGrid.Application.Tests/Hud/GetHudTelemetryQueryTests.cs` (new)

Dependencies: `IUserProfileRepository` (NSubstitute mock), `IWeekRepository` (NSubstitute mock).

| Test | Setup | Assert |
|---|---|---|
| `NoProfile_ReturnsAllZeroDto` | `profileRepo` returns `null` | `IsSuccess=true`; DTO `Level=0`, all numerics 0 |
| `WithProfileNoActiveWeek_ReturnsLifetimeMetrics_WeeklyAllZero` | Profile has `CurrentLevel=5, LifetimeXp=1000, CurrentSp=10`; `weekRepo` returns `null` | n1=5, n4=1000, n6=10; n3=0.0, n5=0, n7=0 |
| `WithMultipleWeekGoals_AveragesGpCorrectly` | Two WeekGoals: `GoalWeeklyGp=2.0` and `GoalWeeklyGp=4.0` | `result.Value.WeeklyGp == 3.0` |
| `WithMultipleWeekGoals_SumsXpCorrectly` | Two WeekGoals: `GoalWeeklyXpEarned=100` and `GoalWeeklyXpEarned=150` | `result.Value.WeeklyXp == 250` |

Helper: build a mock `WeekEntity` whose `WeekGoals` property returns a known list. Since `Week.WeekGoals` is backed by a private `List<WeekGoal>` populated via EF — in tests, use NSubstitute to mock `IWeekRepository.GetActiveAsync()` to return a real `Week` instance with `WeekGoal`s added by reflection/constructor, OR mock the repository to return a `Week`-like object.

**Practical approach:** Because `Week.WeekGoals` is a `private` backing list populated internally, the test helper should create a `Week` via `Week.Create(...)` then use `WeekGoal.Create(weekId, goalId)` objects returned directly from a mocked `IWeekRepository`. The mock returns a stub `WeekEntity?` — but since `WeekEntity` is a sealed class with private setters, the handler must work through the navigation property.

**Alternative:** Mock `IWeekRepository` to return a `null` week and test the weekly path with the week's `WeekGoals` collection. Since `Week` does not have a public `AddWeekGoal` method visible outside the domain, expose test data through a dedicated test helper that calls `Week.Create(...)` and then uses EF Core's `Entry(week).Collection(w => w.WeekGoals).CurrentValue.Add(wg)` pattern — **too complex**.

**Simplest correct approach:** Mock `IWeekRepository.GetActiveAsync()` to return `null` for the no-week tests. For the weekly math tests, the handler accesses `week.WeekGoals` which is a navigation property loaded by the Infrastructure layer. In Application unit tests, mock the repository to return a `Week` constructed via reflection OR avoid testing the math in Application tests and instead cover it in Infrastructure integration tests.

**Decision (follows Phase 6 precedent):** Mock `IWeekRepository.GetActiveAsync()` to return a week where `WeekGoals` is seeded by calling `Week.AddWeekGoal(wg)` — but `AddWeekGoal` is `internal`. To expose it for testing, the handler can be tested with a **custom test-double** that returns a `Week` whose private list is populated via EF Core's navigation scaffolding.

**Pragmatic resolution:** Since the `Week` class exposes `IReadOnlyCollection<WeekGoal> WeekGoals => _weekGoals.AsReadOnly()`, and `_weekGoals` is only set by EF Core or `internal void AddWeekGoal(...)`, the Application test project (which references `LifeGrid.Domain`) cannot populate `WeekGoals` without `InternalsVisibleTo`. 

**Final approach:** Add `[assembly: InternalsVisibleTo("LifeGrid.Application.Tests")]` to `LifeGrid.Domain` — same pattern used for infrastructure tests. This allows the Application test helper to call `week.AddWeekGoal(wg)` directly. The four tests then work with real `Week`/`WeekGoal` domain objects.

---

## Phase B — Infrastructure Layer

### B1 — `WeekRepository.GetActiveAsync()`

**File:** `src/LifeGrid.Infrastructure/Data/Repositories/WeekRepository.cs`

Add:
```csharp
public Task<WeekEntity?> GetActiveAsync(CancellationToken ct = default)
    => db.Weeks
         .Include(w => w.WeekGoals)
         .FirstOrDefaultAsync(w => w.Status == WeekStatus.Active, ct);
```

Required usings: `LifeGrid.Domain.Week` (for `WeekStatus`), `Microsoft.EntityFrameworkCore`.

No new migration — `WeekGoals` navigation is already configured in `WeekConfiguration.HasMany`.

---

## Phase C — Presentation Layer

### C1 — `HudViewModel`

**File:** `src/LifeGrid.Presentation/ViewModels/HudViewModel.cs` (new)

**Why `AppShellViewModel` is injected:** `HudView.xaml` has a `DataTrigger` on the profile icon that fires when `IsProfileActive = true` (set by `CreateGoalPage` on appear/disappear). Once `HudView.BindingContext` changes to `HudViewModel`, the DataTrigger needs `IsProfileActive` from `HudViewModel` rather than from `AppShellViewModel`. Injecting `AppShellViewModel` and forwarding property-change events is the simplest reliable solution in MAUI (avoids fragile `RelativeSource AncestorType={x:Type Shell}` bindings inside `TitleView`).

```csharp
public partial class HudViewModel : ObservableObject
{
    private readonly IMediator         _mediator;
    private readonly AppShellViewModel _appShell;

    public HudViewModel(IMediator mediator, AppShellViewModel appShellViewModel)
    {
        _mediator = mediator;
        _appShell = appShellViewModel;
        _appShell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppShellViewModel.IsProfileActive))
                OnPropertyChanged(nameof(IsProfileActive));
        };
    }

    // Passthrough — keeps the existing DataTrigger in HudView.xaml working
    public bool IsProfileActive => _appShell.IsProfileActive;

    [ObservableProperty] private string _level         = "0";
    [ObservableProperty] private string _gpLifetime    = "0.00";
    [ObservableProperty] private string _gpWeekly      = "0.00";
    [ObservableProperty] private string _xpLifetime    = "0";
    [ObservableProperty] private string _xpWeekly      = "0";
    [ObservableProperty] private string _spCurrent     = "0";
    [ObservableProperty] private string _spWeekly      = "0";
    [ObservableProperty] private string _shieldsActive = "0";
    [ObservableProperty] private string _shieldsCap    = "0";

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetHudTelemetryQuery(), ct);
        if (!result.IsSuccess) return;
        var d      = result.Value!;
        Level         = d.Level.ToString();
        GpLifetime    = d.LifetimeGp.ToString("F2");
        GpWeekly      = d.WeeklyGp.ToString("F2");
        XpLifetime    = d.LifetimeXp.ToString();
        XpWeekly      = d.WeeklyXp.ToString();
        SpCurrent     = d.CurrentSp.ToString();
        SpWeekly      = d.WeeklySpEarned.ToString();
        ShieldsActive = d.ActiveShields.ToString();
        ShieldsCap    = d.ShieldCap.ToString();
    }
}
```

### C2 — `MauiProgram.cs`

Add:
```csharp
builder.Services.AddSingleton<HudViewModel>();
```

Place next to the existing `AppShellViewModel` singleton registration.

### C3 — `AppShell.xaml`

**File:** `src/LifeGrid.Presentation/AppShell.xaml`

Add `x:Name="HudControl"` to the existing `<controls:HudView>` element inside `Shell.TitleView`:
```xml
<Shell.TitleView>
    <controls:HudView x:Name="HudControl" />
</Shell.TitleView>
```

### C4 — `AppShell.xaml.cs`

**File:** `src/LifeGrid.Presentation/AppShell.xaml.cs`

Add `HudViewModel hudViewModel` parameter to the constructor; after `InitializeComponent()` set:
```csharp
HudControl.BindingContext = hudViewModel;
```

Full constructor:
```csharp
public AppShell(AppShellViewModel viewModel, HudViewModel hudViewModel)
{
    InitializeComponent();
    BindingContext = viewModel;
    HudControl.BindingContext = hudViewModel;
    Routing.RegisterRoute("create-goal", typeof(CreateGoalPage));
    Routing.RegisterRoute("user-setup",  typeof(UserSetupPage));
}
```

### C5 — `HudView.xaml` Rebuild

**File:** `src/LifeGrid.Presentation/Controls/HudView.xaml`

Replace the existing `Grid` children entirely:

**Column 0 — Left anchor** (was a single `Label`):
```xml
<HorizontalStackLayout Grid.Column="0" Spacing="4" VerticalOptions="Center">

    <Label
        Text="&#xE853;"
        Style="{StaticResource IconStyle}"
        VerticalOptions="Center">
        <Label.Triggers>
            <DataTrigger TargetType="Label"
                         Binding="{Binding IsProfileActive}"
                         Value="True">
                <Setter Property="TextColor" Value="{StaticResource Primary}" />
            </DataTrigger>
        </Label.Triggers>
        <Label.GestureRecognizers>
            <TapGestureRecognizer Tapped="OnProfileTapped" />
        </Label.GestureRecognizers>
    </Label>

    <Border
        WidthRequest="28"
        HeightRequest="28"
        StrokeThickness="1.5"
        Stroke="{StaticResource Primary}"
        BackgroundColor="Transparent"
        VerticalOptions="Center">
        <Border.StrokeShape>
            <Ellipse />
        </Border.StrokeShape>
        <Label
            Text="{Binding Level}"
            FontFamily="ShareTechMono"
            FontSize="11"
            HorizontalOptions="Center"
            VerticalOptions="Center"
            TextColor="{StaticResource OnSurface}" />
    </Border>

</HorizontalStackLayout>
```

**Column 1 — Center telemetry** (was an empty `ContentView`):
```xml
<ScrollView Grid.Column="1" Orientation="Horizontal" VerticalOptions="Center">
    <HorizontalStackLayout Spacing="16" Padding="8,0">

        <Label VerticalOptions="Center">
            <Label.FormattedText>
                <FormattedString>
                    <Span Text="GP " TextColor="{StaticResource OnSurface}" FontFamily="ShareTechMono" />
                    <Span Text="{Binding GpLifetime}" TextColor="{StaticResource OnSurface}" FontFamily="ShareTechMono" />
                    <Span Text=" / " TextColor="{StaticResource OnSurface}" FontFamily="ShareTechMono" />
                    <Span Text="{Binding GpWeekly}" TextColor="{StaticResource Primary}" FontFamily="ShareTechMono" FontAttributes="Bold" />
                </FormattedString>
            </Label.FormattedText>
        </Label>

        <Label VerticalOptions="Center">
            <Label.FormattedText>
                <FormattedString>
                    <Span Text="XP " TextColor="{StaticResource OnSurface}" FontFamily="ShareTechMono" />
                    <Span Text="{Binding XpLifetime}" TextColor="{StaticResource OnSurface}" FontFamily="ShareTechMono" />
                    <Span Text=" / " TextColor="{StaticResource OnSurface}" FontFamily="ShareTechMono" />
                    <Span Text="{Binding XpWeekly}" TextColor="{StaticResource Primary}" FontFamily="ShareTechMono" FontAttributes="Bold" />
                </FormattedString>
            </Label.FormattedText>
        </Label>

        <Label VerticalOptions="Center">
            <Label.FormattedText>
                <FormattedString>
                    <Span Text="SP " TextColor="{StaticResource OnSurface}" FontFamily="ShareTechMono" />
                    <Span Text="{Binding SpCurrent}" TextColor="{StaticResource OnSurface}" FontFamily="ShareTechMono" />
                    <Span Text=" / " TextColor="{StaticResource OnSurface}" FontFamily="ShareTechMono" />
                    <Span Text="{Binding SpWeekly}" TextColor="{StaticResource Primary}" FontFamily="ShareTechMono" FontAttributes="Bold" />
                </FormattedString>
            </Label.FormattedText>
        </Label>

        <Label VerticalOptions="Center">
            <Label.FormattedText>
                <FormattedString>
                    <Span Text="Shields " TextColor="{StaticResource OnSurface}" FontFamily="ShareTechMono" />
                    <Span Text="{Binding ShieldsActive}" TextColor="{StaticResource Primary}" FontFamily="ShareTechMono" FontAttributes="Bold" />
                    <Span Text=" / " TextColor="{StaticResource OnSurface}" FontFamily="ShareTechMono" />
                    <Span Text="{Binding ShieldsCap}" TextColor="{StaticResource OnSurface}" FontFamily="ShareTechMono" />
                </FormattedString>
            </Label.FormattedText>
        </Label>

    </HorizontalStackLayout>
</ScrollView>
```

**Column 2 — Notifications icon**: unchanged.

**Note on profile icon `DataTrigger`:** The trigger binding `{Binding IsProfileActive}` is preserved as-is. `HudViewModel.IsProfileActive` is a passthrough property (see C1) that mirrors `AppShellViewModel.IsProfileActive` — so the trigger continues to work correctly after the BindingContext change.

### C6 — `HudView.xaml.cs` Fix

**File:** `src/LifeGrid.Presentation/Controls/HudView.xaml.cs`

Change `OnProfileTapped` to read from `Shell.Current` instead of `this.BindingContext`:

```csharp
private async void OnProfileTapped(object? sender, TappedEventArgs e)
{
    if (Shell.Current?.BindingContext is not AppShellViewModel { IsOnboardingComplete: true })
        return;
    await Shell.Current.GoToAsync("user-setup");
}
```

### C7 — `App.xaml.cs` Update

**File:** `src/LifeGrid.Presentation/App.xaml.cs`

- Add `HudViewModel hudViewModel` constructor parameter; store as `_hudViewModel`.
- In `OnStart()`, call `await _hudViewModel.LoadAsync()` in both navigation branches — after `SetOnboardingComplete()` (returning user) and after the session check (new user):

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
        await _hudViewModel.LoadAsync();
        await Shell.Current.GoToAsync("//goals");
        return;
    }

    await _hudViewModel.LoadAsync();
    var sessionResult = await _mediator.Send(new GetOrCreateOnboardingSessionQuery());
    if (sessionResult.IsSuccess && !sessionResult.Value!.IsComplete)
        await Shell.Current.GoToAsync("create-goal");
}
```

---

## Execution Order

```
A1 (HudTelemetryDto) → A2 (IWeekRepository extension) → A3 (GetHudTelemetryQuery) → A4 (tests)
B1 (WeekRepository.GetActiveAsync — depends on A2)
C1 (HudViewModel — depends on A1, A3) → C2 (DI registration)
C3 (AppShell.xaml x:Name) → C4 (AppShell.xaml.cs wiring — depends on C1, C3)
C5 (HudView.xaml rebuild) → C6 (HudView.xaml.cs fix)
C7 (App.xaml.cs — depends on C1)
Build + Full test run
```

---

## Files Changed / Created Summary

| Action | File |
|---|---|
| New | `src/LifeGrid.Application/Hud/HudTelemetryDto.cs` |
| New | `src/LifeGrid.Application/Hud/GetHudTelemetryQuery.cs` |
| Edit | `src/LifeGrid.Application/Week/IWeekRepository.cs` |
| Edit | `src/LifeGrid.Infrastructure/Data/Repositories/WeekRepository.cs` |
| New | `src/LifeGrid.Presentation/ViewModels/HudViewModel.cs` |
| Edit | `src/LifeGrid.Presentation/MauiProgram.cs` |
| Edit | `src/LifeGrid.Presentation/AppShell.xaml` |
| Edit | `src/LifeGrid.Presentation/AppShell.xaml.cs` |
| Edit | `src/LifeGrid.Presentation/Controls/HudView.xaml` |
| Edit | `src/LifeGrid.Presentation/Controls/HudView.xaml.cs` |
| Edit | `src/LifeGrid.Presentation/App.xaml.cs` |
| New | `tests/LifeGrid.Application.Tests/Hud/GetHudTelemetryQueryTests.cs` |

**No new EF Core migration.**

---

## Open Implementation Note (Resolved)

`Week.AddWeekGoal()` is `internal`. Resolved by adding `InternalsVisibleTo("LifeGrid.Application.Tests")` and `InternalsVisibleTo("DynamicProxyGenAssembly2")` to `LifeGrid.Domain.csproj`. Also added `WeekGoal.SetGoalWeeklyGp(double)` and `WeekGoal.SetGoalWeeklyXpEarned(int)` as `internal` test-seeding methods.

---

## As-Built Notes (2026-06-18)

### Corrections vs. Plan

| Item | Planned | As Built |
|---|---|---|
| GP formatting | `.ToString("F2")` | `((int)Math.Ceiling(value)).ToString()` — integer ceiling, no decimals |
| Bonus shield on first goal | Not in plan | Added to `GenerateHabitsCommand`: checks `GetActiveCountAsync == 1`, calls `userProfile.GrantBonusShield()` before `CommitAsync`. New domain methods `UserEconomy.GrantShield()` + `UserProfile.GrantBonusShield()`. |
| `UserSetupPage` profile active state | Not in plan | `OnAppearing` sets `IsProfileActive = true`; `OnDisappearing` resets to `false`. |
| Navigation mutual exclusivity | Not in plan | `AppShell.xaml.cs` subscribes to `IsProfileActive` PropertyChanged; calls `Shell.SetTabBarForegroundColor` + `Shell.SetTabBarTitleColor` with `OnSurface` when profile is active so no tab appears selected simultaneously. |
| `InternalsVisibleTo` for tests | `LifeGrid.Application.Tests` noted in plan | Also added `DynamicProxyGenAssembly2` (NSubstitute proxy runtime). |

### Final Test Count

| Layer | Tests |
|---|---|
| Domain | 61 |
| Application | 50 |
| Infrastructure | 46 |
| **Total** | **157** |
