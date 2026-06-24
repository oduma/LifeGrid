# Phase 24 — Ancillary Actions: Vice Survey Hook & Timeline Pausing

**Status:** DONE  
**Requirements Reference:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` §P24.1–P24.14  
**Spec References:** Functional Requirements 4.1.1 (Vice Survey hook), 4.3.2 (Pause Mechanisms)

---

## Implementation Notes (Post-Approval Corrections)

Three deviations from the approved plan were identified during implementation:

**1. `effectiveTarget` type (`double`, not `int`) — P24.5**
The plan specified `(int)Math.Ceiling(habit.TargetValue * 0.7)` for the effective target. `habit.TargetValue` is `double`; assigning to `int` produces CS0266. Fixed by declaring `double effectiveTarget = week.IsReEntryWeek ? Math.Ceiling(habit.TargetValue * 0.7) : habit.TargetValue;`. The `CalculateWeekGoalGp` projection applies the effective target only to the specific habit being logged; other habits in the same WeekGoal retain their stored `TargetValue`.

**2. Action button container (`VerticalStackLayout` outer guard) — P24.12**
The plan had HIBERNATE WEEK and FREEZE WEEK buttons standalone with `IsVisible="{Binding CanHibernate}"` / `"{Binding CanFreeze}"`. Without an `IsSelected` outer guard, these buttons would appear on non-selected cards. Fixed by wrapping all three action buttons (VIEW WEEK DETAIL, HIBERNATE WEEK, FREEZE WEEK) in a single `<VerticalStackLayout IsVisible="{Binding IsSelected}">`. The inner buttons then filter further via `CanHibernate`/`CanFreeze`.

**3. GoalsViewModel visibility tests — not created (P24.13)**
`GoalsViewModel` is in `LifeGrid.Presentation` (targets `net10.0-android`). The `LifeGrid.Application.Tests` project targets `net10.0`; cross-framework project references are unsupported. A dedicated `LifeGrid.Presentation.Tests` project would be needed. Deferred. The `GetViceSurveyAvailabilityQuery` logic is fully covered by the 3 query handler tests. Test count: **298** (12 new, not 13).

---

## Pre-Flight Analysis (LEAD_ARCHITECT)

### Bounded Contexts Touched
| Context | Change |
|---|---|
| GoalManagement | Vice Survey availability query; GoalsViewModel banner |
| BehavioralEconomy | PauseWeekCommand; Shield consumption; Re-Entry week detection |
| InteractionLog | No change |

### New Artifacts
| Type | Name | Location |
|---|---|---|
| Query | `GetViceSurveyAvailabilityQuery` | `LifeGrid.Application/Vice/` |
| Command | `PauseWeekCommand` | `LifeGrid.Application/Week/` |
| Domain Method | `Week.Pause()`, `Week.MarkAsReEntry()` | `LifeGrid.Domain/Week/` |
| Domain Method | `UserEconomy.ConsumeShield()`, `UserProfile.ConsumeShield()` | `LifeGrid.Domain/UserProfile/` |
| Repository Method | `IWeekRepository.GetByWeekNumberAsync()` | `LifeGrid.Application/Week/` |
| EF Migration | `Phase24_AddIsReEntryWeek` | `LifeGrid.Infrastructure/Migrations/` |

### Clarification Decisions Recorded
- **Re-Entry Week:** In scope for Phase 24 (user decision).
- **Pause UI Pattern:** Buttons appear on the selected Timeline card (consistent with existing VIEW WEEK DETAIL pattern).
- **Freeze Confirmation:** Confirmation dialog required before executing Freeze.
- **Vice Survey Placement:** Top-of-list banner above the Goals CollectionView.

---

## Implementation Phases

### Phase 1 — Domain Layer (SENIOR_NET_DEVELOPER + GAMIFICATION_SPECIALIST)

**Step 1.1 — `Week` entity mutations**

File: `src/LifeGrid.Domain/Week/Week.cs`

Add property:
```csharp
public bool IsReEntryWeek { get; private set; }
```

Add methods:
```csharp
public void Pause(WeekStatus status)
{
    if (status == WeekStatus.Active)
        throw new InvalidOperationException("Cannot pause a week to Active status.");
    Status = status;
}

public void MarkAsReEntry() => IsReEntryWeek = true;
```

**Step 1.2 — `UserEconomy` shield deduction**

File: `src/LifeGrid.Domain/UserProfile/UserEconomy.cs`

Add method:
```csharp
internal bool ConsumeShield()
{
    if (ShieldsAvailable <= 0) return false;
    ShieldsAvailable--;
    return true;
}
```

**Step 1.3 — `UserProfile` delegating method**

File: `src/LifeGrid.Domain/UserProfile/UserProfile.cs`

Add method:
```csharp
public bool ConsumeShield() => Economy.ConsumeShield();
```

---

### Phase 2 — Application Layer (SENIOR_NET_DEVELOPER)

**Step 2.1 — `GetViceSurveyAvailabilityQuery`**

File: `src/LifeGrid.Application/Vice/GetViceSurveyAvailabilityQuery.cs`

```csharp
public record GetViceSurveyAvailabilityQuery : IRequest<Result<bool>>;

public sealed class GetViceSurveyAvailabilityQueryHandler(
    IUserProfileRepository userProfileRepository)
    : IRequestHandler<GetViceSurveyAvailabilityQuery, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        GetViceSurveyAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var profile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (profile is null || profile.IsViceSurveyCompleted)
            return Result<bool>.Success(false);
        return Result<bool>.Success(true);
    }
}
```

**Step 2.2 — `IWeekRepository` addition**

File: `src/LifeGrid.Application/Week/IWeekRepository.cs`

Add:
```csharp
Task<WeekEntity?> GetByWeekNumberAsync(int weekNumber, CancellationToken ct = default);
```

**Step 2.3 — `PauseWeekCommand`**

File: `src/LifeGrid.Application/Week/PauseWeekCommand.cs`

```csharp
public record PauseWeekCommand(Guid WeekId, WeekStatus PauseType) : IRequest<Result>;
```

Handler dependencies: `IWeekRepository`, `IUserProfileRepository`, `IDateTimeProvider`, `IUnitOfWork`, `IEconomyStateBroadcaster`.

Handler logic (see §P24.3 in FUNCTIONAL_REQUIREMENTS.md for full pseudocode):

**Hibernate path:**
1. Load week by `WeekId`; failure if null.
2. Guard: `StartDate > today`; else `"week_already_started"`.
3. `week.Pause(Hibernated)`.
4. Commit + Broadcast.

**Freeze path:**
1. Load week by `WeekId`; failure if null.
2. Guard: `StartDate ≤ today`; else `"week_not_started"`.
3. Guard: `today.DayOfWeek < Friday`; else `"freeze_window_closed"`.
4. Load profile; `profile.ConsumeShield()` returns `true`; else `"no_shields"`.
5. `week.Pause(Frozen)`.
6. Re-entry check: if `GetByWeekNumberAsync(weekNumber - 1)?.Status == Frozen`, call `GetByWeekNumberAsync(weekNumber + 1)?.MarkAsReEntry()`.
7. Commit + Broadcast.

**Step 2.4 — `TimelineWeekDto` update**

File: `src/LifeGrid.Application/Timeline/TimelineWeekDto.cs`

Add `bool IsReEntryWeek` field.

File: `src/LifeGrid.Application/Timeline/GetTimelineQuery.cs`

Map `t.Week.IsReEntryWeek` to the DTO in the handler's `Select` projection.

**Step 2.5 — Re-entry week scaling in `LogHabitProgressCommandHandler`**

File: `src/LifeGrid.Application/HabitLogging/LogHabitProgressCommandHandler.cs`

After loading `week`, derive effective target before GP calculations:
```csharp
var effectiveTarget = week.IsReEntryWeek
    ? (int)Math.Ceiling(habit.TargetValue * 0.7)
    : habit.TargetValue;
```

Pass `effectiveTarget` to `GamificationCalculationEngine.CalculateEntryReward(...)` and use it inside the `CalculateWeekGoalGp(...)` completion summaries (replacing the stored `TargetValue` for this week only).

---

### Phase 3 — Infrastructure Layer (SENIOR_NET_DEVELOPER)

**Step 3.1 — `WeekRepository` new method**

File: `src/LifeGrid.Infrastructure/Data/Repositories/WeekRepository.cs`

```csharp
public Task<WeekEntity?> GetByWeekNumberAsync(int weekNumber, CancellationToken ct = default)
    => db.Weeks
         .Include(w => w.WeekGoals)
         .FirstOrDefaultAsync(w => w.WeekNumber == weekNumber, ct);
```

**Step 3.2 — `WeekConfiguration` update**

File: `src/LifeGrid.Infrastructure/Data/EntityConfigurations/WeekConfiguration.cs`

Inside `Configure`, add:
```csharp
builder.Property(w => w.IsReEntryWeek);
```

**Step 3.3 — EF migration**

Run:
```
dotnet ef migrations add Phase24_AddIsReEntryWeek --project src/LifeGrid.Infrastructure --startup-project src/LifeGrid.Presentation
```

Verify generated migration: `IsReEntryWeek` column `BIT NOT NULL DEFAULT 0` (or `INTEGER NOT NULL DEFAULT 0` for SQLite) on the `Weeks` table. Rollback `Down()` must `DropColumn`.

---

### Phase 4 — Presentation Layer (MAUI_UX_ENGINEER)

**Step 4.1 — `GoalsViewModel` updates**

File: `src/LifeGrid.Presentation/ViewModels/GoalsViewModel.cs`

1. Add `[ObservableProperty] private bool _isViceSurveyBannerVisible;`.
2. Add `GetViceSurveyAvailabilityQuery` call at the end of `LoadAsync()`:
   ```csharp
   var availResult = await mediator.Send(new GetViceSurveyAvailabilityQuery());
   IsViceSurveyBannerVisible = availResult.IsSuccess && availResult.Value;
   ```
3. Add command:
   ```csharp
   [RelayCommand]
   private async Task LaunchViceSurveyAsync()
   {
       var result = await mediator.Send(new LaunchViceSurveyCommand());
       if (!result.IsSuccess) return;
       await Shell.Current.GoToAsync("vice-survey");
   }
   ```

**Step 4.2 — `GoalsPage.xaml` banner**

File: `src/LifeGrid.Presentation/Pages/GoalsPage.xaml`

Restructure inner Grid from `RowDefinitions="*,Auto"` to `RowDefinitions="Auto,*,Auto"`.  
Shift CollectionView to `Grid.Row="1"`, action row to `Grid.Row="2"`.

Add in Row 0:
```xml
<!-- Vice Survey banner — collapses once survey is completed -->
<Border
    Grid.Row="0"
    IsVisible="{Binding IsViceSurveyBannerVisible}"
    Margin="16,8,16,0"
    Padding="12"
    BackgroundColor="{StaticResource Secondary}"
    StrokeShape="RoundRectangle 2"
    StrokeThickness="1"
    Stroke="{StaticResource Error}">
    <Border.GestureRecognizers>
        <TapGestureRecognizer Command="{Binding LaunchViceSurveyCommand}" />
    </Border.GestureRecognizers>
    <Grid ColumnDefinitions="Auto,*">
        <Label
            Grid.Column="0"
            Text="&#xe002;"
            FontFamily="MaterialSymbolsRounded"
            FontSize="20"
            TextColor="{StaticResource OnSecondary}"
            VerticalOptions="Center"
            Margin="0,0,8,0" />
        <VerticalStackLayout Grid.Column="1" Spacing="2">
            <Label
                Text="DETECT HIDDEN VICES"
                FontFamily="{StaticResource FontShareTechMono}"
                FontSize="13"
                FontAttributes="Bold"
                TextColor="{StaticResource OnSecondary}" />
            <Label
                Text="Complete the bad habits survey to earn 1 Bonus Shield"
                FontFamily="{StaticResource FontShareTechMono}"
                FontSize="10"
                TextColor="{StaticResource OnSecondary}"
                Opacity="0.7" />
        </VerticalStackLayout>
    </Grid>
</Border>
```

**Step 4.3 — `TimelineWeekItem` reactive status**

File: `src/LifeGrid.Presentation/ViewModels/TimelineWeekItem.cs`

- Change `Status` from `{ get; init; }` to `[ObservableProperty]` with `[NotifyPropertyChangedFor(nameof(StatusSpText))]`, `[NotifyPropertyChangedFor(nameof(CanHibernate))]`, `[NotifyPropertyChangedFor(nameof(CanFreeze))]`.
- Add `public bool IsReEntryWeek { get; init; }`.
- Add computed properties:
  ```csharp
  public bool CanHibernate => Status == "Active" && StartDate.Date > DateTime.Today;
  public bool CanFreeze    => Status == "Active" && StartDate.Date <= DateTime.Today &&
                               DateTime.Today.DayOfWeek < DayOfWeek.Friday;
  ```

**Step 4.4 — `TimelineViewModel` pause commands**

File: `src/LifeGrid.Presentation/ViewModels/TimelineViewModel.cs`

Add `using LifeGrid.Application.Week;` and `using LifeGrid.Domain.Week;`.

Add to `TimelineViewModel.LoadAsync()` (inside `Weeks.Add(...)` object initializer):
```csharp
IsReEntryWeek = dto.IsReEntryWeek,
```

Add commands:
```csharp
[RelayCommand]
private async Task HibernateWeekAsync(TimelineWeekItem item)
{
    var result = await _mediator.Send(new PauseWeekCommand(item.WeekId, WeekStatus.Hibernated));
    if (!result.IsSuccess)
    {
        var msg = result.Error == "week_already_started"
            ? "This week has already started and cannot be hibernated."
            : "Could not hibernate this week.";
        await Shell.Current.CurrentPage.DisplayAlertAsync("Hibernate Failed", msg, "OK");
        return;
    }
    item.Status = WeekStatus.Hibernated.ToString();
}

[RelayCommand]
private async Task FreezeWeekAsync(TimelineWeekItem item)
{
    var confirmed = await Shell.Current.CurrentPage.DisplayAlertAsync(
        "Emergency Freeze",
        "This will consume 1 Life Happens Shield. Continue?",
        "Freeze", "Cancel");
    if (!confirmed) return;

    var result = await _mediator.Send(new PauseWeekCommand(item.WeekId, WeekStatus.Frozen));
    if (!result.IsSuccess)
    {
        var msg = result.Error switch
        {
            "no_shields"          => "You have no Life Happens Shields available.",
            "freeze_window_closed"=> "Emergency Freeze is only available before Friday of the target week.",
            _                     => "Could not freeze this week."
        };
        await Shell.Current.CurrentPage.DisplayAlertAsync("Freeze Failed", msg, "OK");
        return;
    }
    item.Status = WeekStatus.Frozen.ToString();
}
```

**Step 4.5 — `TimelinePage.xaml` pause buttons & re-entry badge**

File: `src/LifeGrid.Presentation/Pages/TimelinePage.xaml`

**Zone A:** restructure from `ColumnDefinitions="*,Auto"` to `ColumnDefinitions="*,Auto,Auto"`. Insert between Col 0 (header) and the existing status label:

```xml
<!-- Re-entry badge — col 1 -->
<Border
    Grid.Column="1"
    Padding="4,2"
    Margin="4,0"
    IsVisible="{Binding IsReEntryWeek}"
    BackgroundColor="{StaticResource Secondary}"
    StrokeShape="RoundRectangle 2"
    StrokeThickness="0">
    <Label
        Text="RETURNING"
        FontFamily="{StaticResource FontShareTechMono}"
        FontSize="10"
        FontAttributes="Bold"
        TextColor="{StaticResource OnSecondary}"
        VerticalOptions="Center" />
</Border>
```

Move the `StatusSpText` label to `Grid.Column="2"` and add new `DataTrigger` elements for Frozen/Hibernated status:

```xml
<DataTrigger TargetType="Label" Binding="{Binding Status}" Value="Frozen">
    <Setter Property="TextColor" Value="{StaticResource Error}" />
    <Setter Property="Opacity" Value="1.0" />
</DataTrigger>
<DataTrigger TargetType="Label" Binding="{Binding Status}" Value="Hibernated">
    <Setter Property="TextColor" Value="{StaticResource OnSurface}" />
    <Setter Property="Opacity" Value="0.4" />
</DataTrigger>
```

**Pause action buttons** — append inside the `VerticalStackLayout` at the bottom of the card, after the existing `VIEW WEEK DETAIL` button:

```xml
<Button
    Text="HIBERNATE WEEK"
    IsVisible="{Binding CanHibernate}"
    BackgroundColor="{StaticResource Secondary}"
    TextColor="{StaticResource OnSecondary}"
    Command="{Binding Source={x:Reference PageRoot}, Path=BindingContext.HibernateWeekCommand}"
    CommandParameter="{Binding .}"
    Margin="0,4,0,0"
    FontFamily="{StaticResource FontShareTechMono}"
    FontSize="11" />

<Button
    Text="FREEZE WEEK"
    IsVisible="{Binding CanFreeze}"
    BackgroundColor="{StaticResource Error}"
    TextColor="{StaticResource Surface}"
    Command="{Binding Source={x:Reference PageRoot}, Path=BindingContext.FreezeWeekCommand}"
    CommandParameter="{Binding .}"
    Margin="0,4,0,0"
    FontFamily="{StaticResource FontShareTechMono}"
    FontSize="11" />
```

---

### Phase 5 — Tests (TDD_SPECIALIST)

Tests must be written **before** the implementation code they exercise.

**File 1:** `tests/LifeGrid.Application.Tests/Vice/GetViceSurveyAvailabilityQueryTests.cs`
- `NoProfile_ReturnsFalse`
- `SurveyCompleted_ReturnsFalse`
- `SurveyNotCompleted_ReturnsTrue`

**File 2:** `tests/LifeGrid.Application.Tests/Week/PauseWeekCommandTests.cs`
- `Hibernate_FutureWeek_Succeeds`
- `Hibernate_AlreadyStartedWeek_ReturnsWeekAlreadyStartedFailure`
- `Freeze_CurrentWeek_BeforeFriday_HasShields_Succeeds_ConsumesOneShield`
- `Freeze_NoShields_ReturnsNoShieldsFailure`
- `Freeze_AfterThursday_ReturnsWindowClosedFailure`
- `Freeze_PreviousWeekWasFrozen_MarksNextWeekAsReEntry`
- `Freeze_PreviousWeekNotFrozen_DoesNotMarkNextAsReEntry`

**File 3:** `tests/LifeGrid.Application.Tests/HabitLogging/LogHabitProgressCommandHandlerTests.cs` (new test added)
- `ReEntryWeek_EffectiveTargetReducedToSeventyPercent`

**File 4:** `tests/LifeGrid.Application.Tests/Vice/` *(or Goals/ if GoalsViewModel tests live in App.Tests)*
- `GoalsViewModel_SurveyNotCompleted_BannerVisible`
- `GoalsViewModel_SurveyCompleted_BannerHidden`

---

## Test Count Projection

| Suite | Baseline | New Tests | Total |
|---|---|---|---|
| Domain | 95 | 0 | 95 |
| Application | 125 | 13 | 138 |
| Infrastructure | 66 | 0 | 66 |
| **Total** | **286** | **13** | **299** |

---

## Dependency & Ordering Notes

1. Domain changes (Phase 1) must land before any Application layer steps that reference `Week.Pause()` or `UserProfile.ConsumeShield()`.
2. EF migration (Phase 3) must run after `WeekConfiguration` is updated; otherwise `dotnet ef migrations add` will miss the `IsReEntryWeek` column.
3. The `TimelineWeekItem.Status` observable property change is purely a Presentation concern and is independent of all other phases.
4. `LogHabitProgressCommandHandler` change (Step 2.5) requires the `Week.IsReEntryWeek` property from Phase 1 but no new infrastructure.

---

## Risk Notes

- **EF change tracking for `Week.Status` (private setter):** Already confirmed — `WeekConfiguration` maps `Status` via Fluent API and EF4+ correctly tracks private setters via reflection. `IsReEntryWeek` will be configured the same way.
- **`TimelineWeekItem.Status` init → observable:** The object initializer syntax `Status = dto.Status` still compiles and routes through the generated observable setter. No callsite changes needed.
- **`CanHibernate`/`CanFreeze` use `DateTime.Today`:** Acceptable in presentation-layer display models. Not in the domain.
- **Re-entry week edge case — no next week exists:** If `GetByWeekNumberAsync(weekNumber + 1)` returns `null` (the next week hasn't been seeded yet), the re-entry marking is skipped silently. The week will not be marked when it is eventually created; this is an accepted limitation for Phase 24.
