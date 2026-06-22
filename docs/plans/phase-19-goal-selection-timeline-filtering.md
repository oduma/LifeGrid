# Phase 19 — Goal Selection & Timeline Filtering
**Status:** DONE (implemented 2026-06-22)

## Source Documents
- Requirements: `docs/requirements/FUNCTIONAL_REQUIREMENTS.md §P19`
- Phase 19 spec: `docs/requirements/Phase-19-requirements.md`
- Spec: `docs/specs/functional-requirements.md §4.2.3`

---

## Clarifications Confirmed (2026-06-22)

| Question | Answer |
|---|---|
| "Add Goal" button during MultiSelect | Hide it; show "View Filtered Timeline" + "Cancel" instead |
| Swipe gestures during MultiSelect | Disabled — prevents accidental Abandon while selecting |

## Post-Implementation Corrections (2026-06-22)

| Area | Planned | Actual |
|---|---|---|
| `LongPressBehavior` attachment point | Inner `Border` | `SwipeView` — SwipeView intercepts Android touch events before child views receive them; attaching to the outermost view is required |
| `LongPressBehavior` handler registration | Subscribe only via `HandlerChanged` | Also call `RegisterNativeLongClick` immediately in `OnAttachedTo` — in a CollectionView the handler is already connected when the behavior is applied, so `HandlerChanged` never fires |
| Tab navigation lifecycle | `ApplyQueryAttributes` fires before `OnAppearing`; `OnAppearing` calls `LoadAsync` | `OnAppearing` fires **first**; `ApplyQueryAttributes` calls `_ = LoadAsync()` directly to reload with the correct filter |
| Final test count | 252 prior + 5 new = 257 | 83 + 110 + 64 = **257** ✓ |

---

## Architecture Overview

### New Types

| Type | Layer | Purpose |
|---|---|---|
| `GoalSelectionMode` (enum) | Presentation | `Standard \| MultiSelect` |
| `LongPressBehavior` (class) | Presentation | Android `LongClick` event bridged to `ICommand`; no new NuGet packages |

### Modified Types

| Type | Change |
|---|---|
| `GetTimelineQuery` | Add `IReadOnlyList<Guid>? FilterGoalIds = null` parameter |
| `GetTimelineQueryHandler` | Filter WeekGoal entities by GoalId before DTO construction |
| `GoalSummaryItem` | Convert positional record → `partial class : ObservableObject`; add `[ObservableProperty] bool _isSelected` |
| `GoalsViewModel` | Add selection state machine (`SelectionMode`, computed booleans, 5 new commands) |
| `GoalsPage.xaml` + `.xaml.cs` | Tap/long-press gestures, checkbox indicator, button toggle, SwipeView disable |
| `TimelineViewModel` | Implement `IQueryAttributable`; add `_filterGoalIds`, `IsFilteredMode`, `SeeAllGoalsCommand` |
| `TimelinePage.xaml` | Add "See all Goals" button row between list and ad banner |

### Key Design Decisions

1. **Filter applied at entity level in handler** — `WeekGoal` entities are filtered by GoalId before DTOs are built; `TimelineWeekGoalDto` needs no new fields.
2. **`GoalSummaryItem.IsSelected` is a property on the item itself** — eliminates the need for a separate `HashSet<Guid>` in the ViewModel and enables direct XAML binding.
3. **`ShellNavigationQueryParameters` (not URL query strings)** — passes `IReadOnlyList<Guid>` as a typed object; avoids GUID serialization/deserialization.
4. **`ApplyQueryAttributes` sets state only; `OnAppearing` loads** — Shell calls `ApplyQueryAttributes` before `OnAppearing` on every navigation; this avoids double-loading without special guards.
5. **`LongPressBehavior` uses `Handler.PlatformView` Android `LongClick`** — single `#if ANDROID` block, no new NuGet packages, consistent with the Zero-Dependency Rule for the presentation target.

---

## Implementation Plan

### Phase A — TDD Stubs (Write Failing Tests First)

**File:** `tests/LifeGrid.Application.Tests/Timeline/GetTimelineQueryFilterTests.cs` (new)

Five tests, all failing until Phase B:

| Test name | What it asserts |
|---|---|
| `NullFilter_ReturnsAllWeeks` | `FilterGoalIds = null` → all weeks returned unchanged |
| `EmptyFilter_ReturnsAllWeeks` | `FilterGoalIds = []` → same as null |
| `SingleGoalFilter_ExcludesNonMatchingGoalItems` | Week with goalA + goalB items; filter by goalA → week returned with only goalA item |
| `SingleGoalFilter_ExcludesWeeksWithZeroMatchingItems` | Week has only goalB; filter by goalA → week excluded |
| `MultiGoalFilter_IncludesWeeksMatchingAnyGoal` | Two weeks (week1: goalA+goalB, week2: goalC); filter [goalA, goalC] → both weeks returned; week1 has only goalA |

**Setup pattern:**
- Mock `IWeekRepository.GetTimelineAsync()` returning `Week` domain aggregates with `WeekGoals` populated.
- Mock `IGoalRepository.GetByIdsAsync()` returning a minimal `Goal` aggregate for each GoalId.
- Verify returned `TimelineWeekDto` list shape, not internal call counts.

> **Note:** Check `Week` aggregate construction (factory method or constructor) before writing the tests — the exact approach depends on whether `Week` supports a mutable `WeekGoals` collection or requires a factory.

> **Presentation ViewModel tests:** `GoalsViewModel` and `TimelineViewModel` live in `LifeGrid.Presentation` (target `net10.0-android`). A Presentation test project targeting `net10.0-android` runs only on an emulator — out of scope for Phase 19. The ViewModel state assertions in section 5 of the Phase 19 spec file are covered by the manual acceptance criteria below.

---

### Phase B — Application: GetTimelineQuery Filter

**File:** `src/LifeGrid.Application/Timeline/GetTimelineQuery.cs`

```csharp
public record GetTimelineQuery(IReadOnlyList<Guid>? FilterGoalIds = null)
    : IRequest<Result<IReadOnlyList<TimelineWeekDto>>>;
```

**File:** `src/LifeGrid.Application/Timeline/GetTimelineQueryHandler.cs`

Update `Handle` to apply filter before building the description map. Replace the current `weeks.SelectMany(...)` goalId extraction with a filtered version:

```csharp
// After: var weeks = await weekRepository.GetTimelineAsync(cancellationToken);
// Insert filter step:
HashSet<Guid>? filterSet = request.FilterGoalIds is { Count: > 0 } f ? f.ToHashSet() : null;

// Adjust the DTO projection loop to filter per week:
var dtos = new List<TimelineWeekDto>();
foreach (var w in weeks)
{
    var goals = filterSet is null
        ? w.WeekGoals.ToList()
        : w.WeekGoals.Where(wg => filterSet.Contains(wg.GoalId)).ToList();

    if (filterSet is not null && goals.Count == 0)
        continue;   // skip this week entirely

    dtos.Add(new TimelineWeekDto(
        w.WeekId,
        w.StartDate,
        w.Status.ToString(),
        w.TotalWeeklySpEarned,
        goals.Select(wg => new TimelineWeekGoalDto(
            descMap.GetValueOrDefault(wg.GoalId, string.Empty),
            wg.PenaltyState.ToString(),
            wg.GoalWeeklyGp,
            wg.GoalWeeklyXpEarned
        )).ToList()
    ));
}
```

> **Important:** Move the goalId extraction (for `descMap`) to use the already-filtered goals collection, not the raw `weeks.SelectMany(...)`, to avoid loading goal descriptions for excluded goals:
> ```csharp
> // Build goalId list AFTER the filter loop (or recompute from dtos):
> var goalIds = dtos.SelectMany(d => /* need access to WeekGoal GoalIds */)...
> ```
> In practice, extract goalIds from the filtered per-week goals list inside the loop, accumulating into a `HashSet<Guid>`, then batch-load descriptions once. Verify the exact refactoring against the current handler code.

**Call-site audit:** `new GetTimelineQuery()` (no arguments) in `TimelineViewModel.LoadAsync` compiles without change because `FilterGoalIds` defaults to `null`.

---

### Phase C — Presentation: GoalSummaryItem → Observable Class

**File:** `src/LifeGrid.Presentation/ViewModels/GoalSummaryItem.cs`

Replace the positional record entirely:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace LifeGrid.Presentation.ViewModels;

public sealed partial class GoalSummaryItem : ObservableObject
{
    public required Guid     GoalId       { get; init; }
    public required string   Description  { get; init; }
    public required string   AmbientTag   { get; init; }
    public required string   Duration     { get; init; }
    public required DateTime DeadlineDate { get; init; }
    public required string   Status       { get; init; }
    public required int      TotalWeeks   { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}
```

**File:** `src/LifeGrid.Presentation/ViewModels/GoalsViewModel.cs` — update `LoadAsync` construction from positional:

```csharp
Goals.Add(new GoalSummaryItem
{
    GoalId       = dto.GoalId,
    Description  = dto.Description,
    AmbientTag   = dto.AmbientTag,
    Duration     = dto.Duration,
    DeadlineDate = dto.DeadlineDate,
    Status       = dto.Status,
    TotalWeeks   = dto.TotalWeeks,
});
```

---

### Phase D — Presentation: GoalSelectionMode Enum

**File:** `src/LifeGrid.Presentation/ViewModels/GoalSelectionMode.cs` (new)

```csharp
namespace LifeGrid.Presentation.ViewModels;

public enum GoalSelectionMode { Standard, MultiSelect }
```

---

### Phase E — Presentation: LongPressBehavior

**File:** `src/LifeGrid.Presentation/Behaviors/LongPressBehavior.cs` (new — create `Behaviors/` folder if absent)

```csharp
using System.Windows.Input;

namespace LifeGrid.Presentation.Behaviors;

public sealed class LongPressBehavior : Behavior<View>
{
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(LongPressBehavior));

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(LongPressBehavior));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.HandlerChanged += OnHandlerChanged;
    }

    protected override void OnDetachingFrom(View bindable)
    {
        bindable.HandlerChanged -= OnHandlerChanged;
        base.OnDetachingFrom(bindable);
    }

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        if (sender is View v && v.Handler?.PlatformView is Android.Views.View nativeView)
            nativeView.LongClick += OnLongClick;
#endif
    }

#if ANDROID
    private void OnLongClick(object? sender, Android.Views.View.LongClickEventArgs e)
    {
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
        e.Handled = true;
    }
#endif
}
```

---

### Phase F — Presentation: GoalsViewModel — Selection State Machine

**File:** `src/LifeGrid.Presentation/ViewModels/GoalsViewModel.cs`

Add the following to the existing class body:

```csharp
// ── Selection state ───────────────────────────────────────────────────────

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(IsMultiSelectMode))]
[NotifyPropertyChangedFor(nameof(IsAddGoalVisible))]
private GoalSelectionMode _selectionMode = GoalSelectionMode.Standard;

public bool IsMultiSelectMode             => _selectionMode == GoalSelectionMode.MultiSelect;
public bool IsAddGoalVisible              => !IsMultiSelectMode;
public bool IsViewFilteredTimelineVisible => IsMultiSelectMode && Goals.Any(g => g.IsSelected);

// ── Selection commands ────────────────────────────────────────────────────

[RelayCommand]
private void EnterMultiSelect(GoalSummaryItem item)
{
    SelectionMode   = GoalSelectionMode.MultiSelect;
    item.IsSelected = true;
    OnPropertyChanged(nameof(IsViewFilteredTimelineVisible));
}

[RelayCommand]
private void ToggleGoalSelection(GoalSummaryItem item)
{
    item.IsSelected = !item.IsSelected;
    OnPropertyChanged(nameof(IsViewFilteredTimelineVisible));
}

[RelayCommand]
private async Task NavigateToGoalTimelineAsync(GoalSummaryItem item)
{
    if (IsMultiSelectMode)
    {
        ToggleGoalSelection(item);
        return;
    }
    await Shell.Current.GoToAsync("//timeline",
        new ShellNavigationQueryParameters
        {
            ["filterGoalIds"] = (IReadOnlyList<Guid>)new[] { item.GoalId }
        });
}

[RelayCommand]
private async Task ViewFilteredTimelineAsync()
{
    var ids = Goals.Where(g => g.IsSelected).Select(g => g.GoalId).ToList();
    ResetSelectionState();
    await Shell.Current.GoToAsync("//timeline",
        new ShellNavigationQueryParameters
        {
            ["filterGoalIds"] = (IReadOnlyList<Guid>)ids
        });
}

[RelayCommand]
private void ExitMultiSelect() => ResetSelectionState();

public void ResetSelectionState()
{
    foreach (var g in Goals) g.IsSelected = false;
    SelectionMode = GoalSelectionMode.Standard;
    OnPropertyChanged(nameof(IsViewFilteredTimelineVisible));
}
```

**File:** `src/LifeGrid.Presentation/Pages/GoalsPage.xaml.cs`

Add `ResetSelectionState()` to `OnAppearing` (before `LoadAsync`):

```csharp
protected override async void OnAppearing()
{
    base.OnAppearing();
    _viewModel.ResetSelectionState();
    await _viewModel.LoadAsync();
}
```

---

### Phase G — Presentation: GoalsPage.xaml

**G1. Add namespace declarations** (top of file, alongside existing `xmlns` entries):
```xml
xmlns:behaviors="clr-namespace:LifeGrid.Presentation.Behaviors"
xmlns:vm="clr-namespace:LifeGrid.Presentation.ViewModels"
```

**G2. Disable SwipeView in MultiSelect mode:**

Change the `IsEnabled` binding from Status-converter to `IsAddGoalVisible`:
```xml
<SwipeView IsEnabled="{Binding Source={x:Reference Root}, Path=BindingContext.IsAddGoalVisible}">
```
`IsAddGoalVisible` is `true` in Standard mode (swipes enabled) and `false` in MultiSelect (swipes suppressed).

**G3. Add TapGestureRecognizer to the goal card `Border`** (inside the existing `SwipeView`):

Add a `<Border.GestureRecognizers>` block:
```xml
<Border.GestureRecognizers>
    <TapGestureRecognizer
        Command="{Binding Source={x:Reference Root}, Path=BindingContext.NavigateToGoalTimelineCommand}"
        CommandParameter="{Binding .}" />
</Border.GestureRecognizers>
```

**G4. Add LongPressBehavior to the same `Border`:**

Add a `<Border.Behaviors>` block:
```xml
<Border.Behaviors>
    <behaviors:LongPressBehavior
        Command="{Binding Source={x:Reference Root}, Path=BindingContext.EnterMultiSelectCommand}"
        CommandParameter="{Binding .}" />
</Border.Behaviors>
```

**G5. Add checkbox indicator in Zone A (card header Grid):**

The existing Zone A is `<Grid ColumnDefinitions="*,Auto">`. Add a third column and a new Label:

Change column definitions:
```xml
<Grid ColumnDefinitions="*,Auto,Auto">
```

Add after the Status badge `Border` (Grid.Column="1"):
```xml
<Label
    Grid.Column="2"
    Text="&#xE834;"
    FontFamily="MaterialSymbolsRounded"
    FontSize="22"
    TextColor="{StaticResource Primary}"
    IsVisible="{Binding IsSelected}"
    VerticalOptions="Center"
    Margin="8,0,0,0" />
```

**G6. Replace bottom "Add Goal" button with mode-sensitive action area:**

The inner `Grid` (Grid.Row="0" of the outer Grid) currently has `RowDefinitions="*,Auto"` with:
- Row 0: CollectionView
- Row 1: `<Button Text="Add Goal" ...>`

Replace the `Button` at Grid.Row="1" with:
```xml
<Grid Grid.Row="1" Margin="16,8">
    <!-- Standard mode -->
    <Button
        Text="Add Goal"
        IsVisible="{Binding IsAddGoalVisible}"
        Command="{Binding AddGoalCommand}" />

    <!-- MultiSelect mode -->
    <VerticalStackLayout
        IsVisible="{Binding IsMultiSelectMode}"
        Spacing="8">
        <Button
            Text="View Filtered Timeline"
            IsEnabled="{Binding IsViewFilteredTimelineVisible}"
            Command="{Binding ViewFilteredTimelineCommand}" />
        <Button
            Text="Cancel"
            Command="{Binding ExitMultiSelectCommand}" />
    </VerticalStackLayout>
</Grid>
```

---

### Phase H — Presentation: TimelineViewModel — Filter Support

**File:** `src/LifeGrid.Presentation/ViewModels/TimelineViewModel.cs`

1. Add `IQueryAttributable` to the class declaration:
```csharp
public sealed partial class TimelineViewModel(IMediator mediator)
    : ObservableObject, IQueryAttributable
```

2. Add filter state and `SeeAllGoals` command:
```csharp
private IReadOnlyList<Guid>? _filterGoalIds;

[ObservableProperty]
private bool _isFilteredMode;

public void ApplyQueryAttributes(IDictionary<string, object> query)
{
    if (query.TryGetValue("filterGoalIds", out var value) &&
        value is IReadOnlyList<Guid> { Count: > 0 } ids)
    {
        _filterGoalIds = ids;
        IsFilteredMode = true;
    }
    else
    {
        _filterGoalIds = null;
        IsFilteredMode = false;
    }
    // Do NOT call LoadAsync here — OnAppearing fires immediately after and loads.
}

[RelayCommand]
private async Task SeeAllGoalsAsync()
{
    _filterGoalIds = null;
    IsFilteredMode = false;
    await LoadAsync();
}
```

3. Update `LoadAsync` to pass the current filter:
```csharp
var result = await mediator.Send(new GetTimelineQuery(_filterGoalIds));
```

---

### Phase I — Presentation: TimelinePage.xaml

**I1. Add a new row between list and ad banner:**

Change:
```xml
<Grid RowDefinitions="*,Auto">
```
To:
```xml
<Grid RowDefinitions="*,Auto,Auto">
```

**I2. Shift ad banner to Row 2:**
```xml
<controls:AdBannerView Grid.Row="2" />
```

**I3. Add "See all Goals" button at Row 1:**
```xml
<Button
    Grid.Row="1"
    Text="See all Goals"
    IsVisible="{Binding IsFilteredMode}"
    Command="{Binding SeeAllGoalsCommand}"
    Margin="16,8" />
```

---

### Phase J — Build & Verify

```
dotnet build LifeGrid.slnx
dotnet test
```

Expected: 0 build errors; 252 prior tests + 5 new filter tests = **257 passed**.

**Manual verification checklist:**

1. Tap a Goal Card (Standard mode) → Timeline shows only that goal's weeks; "See all Goals" button visible.
2. "See all Goals" → full timeline restored; button hidden.
3. Long-press a Goal Card → MultiSelect mode: "Add Goal" hidden; swipes disabled; checkbox appears on card.
4. Tap further cards → checkbox toggles on each.
5. "View Filtered Timeline" (disabled if none selected, enabled if ≥1) → filtered Timeline; Goals page resets to Standard.
6. "Cancel" → Standard mode; "Add Goal" visible; swipes re-enabled; all checkboxes cleared.
7. Switch tabs away and back to Goals → Standard mode automatically (no stale selection).
8. Filtered Timeline with two goals → weeks for either goal included; weeks with neither excluded.

---

## Files Changed

| File | Change |
|---|---|
| `src/LifeGrid.Application/Timeline/GetTimelineQuery.cs` | Add `FilterGoalIds` parameter |
| `src/LifeGrid.Application/Timeline/GetTimelineQueryHandler.cs` | Filter WeekGoal entities before DTO construction |
| `src/LifeGrid.Presentation/ViewModels/GoalSummaryItem.cs` | Record → observable partial class; add `IsSelected` |
| `src/LifeGrid.Presentation/ViewModels/GoalSelectionMode.cs` | **New** |
| `src/LifeGrid.Presentation/Behaviors/LongPressBehavior.cs` | **New** |
| `src/LifeGrid.Presentation/ViewModels/GoalsViewModel.cs` | Selection state machine + 5 new commands |
| `src/LifeGrid.Presentation/Pages/GoalsPage.xaml.cs` | Reset selection in `OnAppearing` |
| `src/LifeGrid.Presentation/Pages/GoalsPage.xaml` | Tap/long-press gestures, checkbox, button toggle, SwipeView disable |
| `src/LifeGrid.Presentation/ViewModels/TimelineViewModel.cs` | `IQueryAttributable`, `_filterGoalIds`, `IsFilteredMode`, `SeeAllGoalsCommand` |
| `src/LifeGrid.Presentation/Pages/TimelinePage.xaml` | "See all Goals" button row |
| `tests/LifeGrid.Application.Tests/Timeline/GetTimelineQueryFilterTests.cs` | **New** — 5 filter tests |
