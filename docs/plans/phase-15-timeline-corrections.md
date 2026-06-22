# Phase 15 — Timeline View Corrections
**Status:** DONE — completed 2026-06-22

## Source Documents
- Requirements: `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` §P15 (to be written after approval)
- Style tokens: `docs/specs/style-guide.md` §2 (Primary `#35f8db`, Secondary `#e5cde1`)

---

## Clarifications Confirmed (2026-06-22)

| Question | Answer |
|---|---|
| Active vs Selected visual when current week is also tapped | **Primary wins** — selected (tapped) card always shows Primary regardless of `IsCurrentWeek` |
| Initial scroll on load | **Scroll to current week** — one-shot `ScrollTo` on `OnAppearing`, no snap paging |
| Semantics of "active" | **Current week is always "active" by date**, not by domain Status string. Tapping a card makes it "selected" (browsing focus, Primary border) but does NOT change which week is the current week. |

---

## Requirements (P15)

### P15.1 — Current Week Identification
`TimelineWeekItem.IsCurrentWeek` is `true` when `item.StartDate == GoalAggregate.CalculateStartDate(DateTime.Today)`. Set once in `LoadAsync`; never mutated after that.

### P15.2 — Current Week Always Prominent
The current week shows Secondary (`#e5cde1`) border at 2 px with full opacity whenever it is NOT the user-selected card. If it IS the selected card, Primary wins (see matrix below).

### P15.3 — Tap to Select
A `TapGestureRecognizer` on each week card fires `SelectWeekCommand` with the tapped `TimelineWeekItem` as the parameter. The ViewModel deselects the previous item and selects the new one. No scroll-position tracking.

### P15.4 — Free Scrolling (No Snap Paging)
`SnapPointsType` and `SnapPointsAlignment` removed from `LinearItemsLayout`. The `Scrolled` event handler and all scroll-driven active-week logic are deleted.

### P15.5 — Auto-Scroll to Current Week on Load
`OnAppearing` calls `WeeksCollection.ScrollTo(_viewModel.CurrentWeekIndex, position: ScrollToPosition.Center, animate: false)`. If no current week is found (empty list), no scroll occurs.

### P15.6 — Visual State Matrix

| `IsSelected` | `IsCurrentWeek` | Stroke | StrokeThickness | Opacity |
|---|---|---|---|---|
| `true` | any | Primary `#35f8db` | 2 | 1.0 |
| `false` | `true` | Secondary `#e5cde1` | 2 | 1.0 |
| `false` | `false` | SurfaceBrush `#ffffff` | 1 | 0.55 |

This is encoded as a single computed string property `CardBorderState` ("Selected" / "Current" / "Default") on `TimelineWeekItem`. A single `DataTrigger` per state (mutually exclusive — no trigger-priority conflicts).

---

## Implementation Plan

### Phase A — TDD Stub
**File:** `tests/LifeGrid.Application.Tests/Timeline/TimelineViewModelTests.cs`

Follow the established pattern from `UserSetupViewModelTests.cs`: add a comment-only file documenting that `TimelineViewModel` and `TimelineWeekItem` live in `LifeGrid.Presentation` (target `net10.0-android`) and cannot be directly referenced from the `net10.0` test project. Data-pipeline coverage is already provided by `GetTimelineQueryHandlerTests.cs`. Full ViewModel branch coverage is deferred to a future `LifeGrid.Presentation.Tests` project.

---

### Phase B — `TimelineWeekItem.cs`

**Remove:** `IsActive` observable property.

**Add:**
```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(CardBorderState))]
[NotifyPropertyChangedFor(nameof(CardOpacity))]
[NotifyPropertyChangedFor(nameof(IsHighlighted))]
private bool _isSelected;

public bool IsCurrentWeek { get; init; }

public string CardBorderState =>
    IsSelected ? "Selected" : IsCurrentWeek ? "Current" : "Default";

public double CardOpacity  => IsSelected || IsCurrentWeek ? 1.0 : 0.55;
public bool   IsHighlighted => IsSelected || IsCurrentWeek;
```

---

### Phase C — `TimelineViewModel.cs`

**Remove:**
- `_activeWeekIndex` field
- `ActiveWeekIndex` property
- `SetActiveWeekByIndex(int)` method
- `FindInitialActiveIndex()` method

**Add/Update:**
```csharp
private int _currentWeekIndex = -1;
private TimelineWeekItem? _selectedWeek;

public int CurrentWeekIndex => _currentWeekIndex;

// In LoadAsync — after Weeks.Clear() / population loop:
var currentMonday = GoalAggregate.CalculateStartDate(DateTime.Today);
for (int i = 0; i < Weeks.Count; i++)
{
    if (Weeks[i].StartDate.Date == currentMonday.Date)
    {
        Weeks[i] = Weeks[i] with { IsCurrentWeek = true }; // or set directly since init
        _currentWeekIndex = i;
        break;
    }
}
// Set initial selection = current week (or last week if not found)
var initialIndex = _currentWeekIndex >= 0 ? _currentWeekIndex : Weeks.Count - 1;
if (initialIndex >= 0)
{
    _selectedWeek = Weeks[initialIndex];
    _selectedWeek.IsSelected = true;
}
```

> **Note:** `IsCurrentWeek` is `init`-only. It must be set in the `TimelineWeekItem` initializer inside `LoadAsync` when constructing the item (not via assignment after construction). Update the `new TimelineWeekItem { ... }` block to include `IsCurrentWeek = (dto.StartDate.Date == currentMonday.Date)`.

**Add `SelectWeekCommand`:**
```csharp
[RelayCommand]
private void SelectWeek(TimelineWeekItem item)
{
    if (_selectedWeek == item) return;
    if (_selectedWeek is not null) _selectedWeek.IsSelected = false;
    _selectedWeek = item;
    item.IsSelected = true;
}
```

---

### Phase D — `TimelinePage.xaml`

**`ContentPage` element:** add `x:Name="TimelinePage"` (needed for TapGestureRecognizer ancestor binding).

**`CollectionView`:** remove `Scrolled="OnCollectionViewScrolled"`.

**`LinearItemsLayout`:** remove `SnapPointsType="MandatorySingle"` and `SnapPointsAlignment="Center"`.

**Week card `Border`:** 
1. Replace `BackgroundColor` with just the default `{StaticResource Surface}`.
2. Set base `Opacity="{Binding CardOpacity}"` (replaces the 0.55 trigger).
3. Replace all existing `Border.Triggers` with:
```xml
<Border.Triggers>
    <DataTrigger TargetType="Border" Binding="{Binding CardBorderState}" Value="Current">
        <Setter Property="Stroke" Value="{StaticResource Secondary}" />
        <Setter Property="StrokeThickness" Value="2" />
    </DataTrigger>
    <DataTrigger TargetType="Border" Binding="{Binding CardBorderState}" Value="Selected">
        <Setter Property="Stroke" Value="{StaticResource Primary}" />
        <Setter Property="StrokeThickness" Value="2" />
    </DataTrigger>
</Border.Triggers>
```
4. Add tap gesture:
```xml
<Border.GestureRecognizers>
    <TapGestureRecognizer
        Command="{Binding Source={x:Reference TimelinePage}, Path=BindingContext.SelectWeekCommand}"
        CommandParameter="{Binding .}" />
</Border.GestureRecognizers>
```

**`StatusSpText` Label:** replace `IsActive` trigger with `IsHighlighted`:
```xml
<Label.Triggers>
    <DataTrigger TargetType="Label" Binding="{Binding IsHighlighted}" Value="False">
        <Setter Property="TextColor" Value="{StaticResource OnSurface}" />
        <Setter Property="Opacity" Value="0.6" />
    </DataTrigger>
</Label.Triggers>
```

---

### Phase E — `TimelinePage.xaml.cs`

**Remove:** `OnCollectionViewScrolled` method.

**Update `OnAppearing`:**
```csharp
protected override async void OnAppearing()
{
    base.OnAppearing();
    await _viewModel.LoadAsync();

    if (_viewModel.CurrentWeekIndex >= 0)
        WeeksCollection.ScrollTo(
            _viewModel.CurrentWeekIndex,
            position: ScrollToPosition.Center,
            animate: false);
}
```

---

### Phase F — Build & Verify

```
dotnet build
dotnet test
```

Expected: 0 build errors; 253+ tests passed (+ 1 new stub test file counted).

---

## Files Changed

| File | Change |
|---|---|
| `src/LifeGrid.Presentation/ViewModels/TimelineWeekItem.cs` | Add `IsCurrentWeek`, `IsSelected`, `CardBorderState`, `CardOpacity`, `IsHighlighted`; remove `IsActive` |
| `src/LifeGrid.Presentation/ViewModels/TimelineViewModel.cs` | Add `SelectWeekCommand`, `CurrentWeekIndex`; rework `LoadAsync`; remove `SetActiveWeekByIndex`, `FindInitialActiveIndex`, `ActiveWeekIndex` |
| `src/LifeGrid.Presentation/Pages/TimelinePage.xaml` | Remove snap layout; add tap gesture; update border + label triggers |
| `src/LifeGrid.Presentation/Pages/TimelinePage.xaml.cs` | Remove `OnCollectionViewScrolled`; update scroll target |
| `tests/LifeGrid.Application.Tests/Timeline/TimelineViewModelTests.cs` | New stub file (TFM constraint comment, per established pattern) |
