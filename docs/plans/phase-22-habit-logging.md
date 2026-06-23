# Phase 22 — Habit Execution & Progress Logging (Preparatory Pipeline)

**Status:** DONE (implemented 2026-06-23)

### Post-Implementation Corrections

| Area | Plan | Actual / Fix |
|---|---|---|
| `HabitLoggingViewModel.IsNotBusy` | `=> !_isBusy` | MVVMTK0034 warning — backing field must not be referenced directly. Fixed to `=> !IsBusy` (generated property). |
| `data-structure.json` | Update if needed | No change required — `Completed_Values_Log` was already fully documented. |

---

## Context

This phase establishes the data-entry workflow for habit completion. A user taps a habit card (Home or Week Detail), a modal form appears, they enter an `ActualValue` + optional proof, and the entry is persisted to `HabitCompletionLogs`. No gamification math is triggered; that comes in a later phase.

**Key decisions (resolved 2026-06-23):**
- Modal: Shell `ContentPage` via `"habit-logging"` route — `OnAppearing` on parent handles refresh
- Proof: both fields independently optional (no XOR in this phase)
- Future-week locking: UI-level enforcement in `WeeklyHabitsPage` (opacity + tap disabled)

---

## Pre-Flight Analysis

### What already exists

| Item | Location | State |
|---|---|---|
| `Habit` aggregate + `CompletedValueLog` entity | `LifeGrid.Domain/Habit/` | Ready — `AddCompletionLog` is `internal`; `Create` hardcodes `DateTime.UtcNow` |
| `HabitCompletionLogs` DB table + EF config | `CompletedValueLogConfiguration.cs` | Ready — `DbSet<CompletedValueLog>` on `LifeGridDbContext` |
| `IHabitRepository` | `LifeGrid.Application/Habit/` | Missing `GetByIdAsync` and `AddCompletionLogAsync` |
| `WeeklyHabitItem` | `Presentation/ViewModels/` | Missing `GoalDescription`, `WeekLabel`, `IsInteractive` |
| `WeeklyGoalGroupItem` | `Presentation/ViewModels/` | Missing `isFuture` propagation |
| `IDateTimeProvider` | `LifeGrid.Application/Common/` | Ready (Phase 21) |

### What must be built

15 source files across 5 phases + 1 test file.

---

## Sequential Phases

---

### Phase 1 — Domain (`SENIOR_NET_DEVELOPER`)

**1 file modified.**

#### 1.1 `CompletedValueLog.Create` signature change

`src/LifeGrid.Domain/Habit/CompletedValueLog.cs`

Change:
```csharp
// BEFORE:
public static CompletedValueLog Create(
    Guid habitId, double actualValue, string measurementUnit,
    string? proofText, string? proofImageUrl) => new()
{
    ...
    Timestamp = DateTime.UtcNow
};

// AFTER:
public static CompletedValueLog Create(
    Guid habitId, double actualValue, string measurementUnit,
    string? proofText, string? proofImageUrl, DateTime timestamp) => new()
{
    ...
    Timestamp = timestamp
};
```

**Rationale:** Domain cannot reference `IDateTimeProvider` (Zero-Dependency Rule). The Application command handler passes `IDateTimeProvider.UtcNow` so tests can control the timestamp.

---

### Phase 2 — Application Layer (`SENIOR_NET_DEVELOPER`)

**3 files: 1 modified, 2 new.**

#### 2.1 Extend `IHabitRepository`

`src/LifeGrid.Application/Habit/IHabitRepository.cs`

Add two methods:
```csharp
Task<HabitEntity?> GetByIdAsync(Guid habitId, CancellationToken ct = default);
Task AddCompletionLogAsync(CompletedValueLog log, CancellationToken ct = default);
```

(Import alias `using CompletedValueLog = LifeGrid.Domain.Habit.CompletedValueLog;`)

#### 2.2 `LogHabitProgressCommand`

**New file:** `src/LifeGrid.Application/HabitLogging/LogHabitProgressCommand.cs`

```csharp
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.HabitLogging;

public record LogHabitProgressCommand(
    Guid    HabitId,
    double  ActualValue,
    string  MeasurementUnit,
    string? ProofText,
    string? ProofImageUrl) : IRequest<Result>;
```

#### 2.3 `LogHabitProgressCommandHandler`

**New file:** `src/LifeGrid.Application/HabitLogging/LogHabitProgressCommandHandler.cs`

```csharp
namespace LifeGrid.Application.HabitLogging;

public sealed class LogHabitProgressCommandHandler(
    IHabitRepository  habitRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork       unitOfWork)
    : IRequestHandler<LogHabitProgressCommand, Result>
{
    public async Task<Result> Handle(
        LogHabitProgressCommand request, CancellationToken cancellationToken)
    {
        if (request.ActualValue <= 0)
            return Result.Failure("Actual value must be greater than zero.");

        var habit = await habitRepository.GetByIdAsync(request.HabitId, cancellationToken);
        if (habit is null)
            return Result.Failure("Habit not found.");

        var log = CompletedValueLog.Create(
            request.HabitId, request.ActualValue, request.MeasurementUnit,
            request.ProofText, request.ProofImageUrl,
            dateTimeProvider.UtcNow);

        await habitRepository.AddCompletionLogAsync(log, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
```

---

### Phase 3 — Infrastructure (`SENIOR_NET_DEVELOPER`)

**1 file modified.**

#### 3.1 `HabitRepository` — implement new interface methods

`src/LifeGrid.Infrastructure/Data/Repositories/HabitRepository.cs`

Add:
```csharp
public Task<HabitEntity?> GetByIdAsync(Guid habitId, CancellationToken ct = default)
    => db.Habits.FirstOrDefaultAsync(h => h.HabitId == habitId, ct);

public Task AddCompletionLogAsync(CompletedValueLog log, CancellationToken ct = default)
{
    db.CompletedValueLogs.Add(log);
    return Task.CompletedTask;
}
```

---

### Phase 4 — Presentation: ViewModels (`MAUI_UX_ENGINEER`)

**5 files: 4 modified, 1 new.**

#### 4.1 `WeeklyHabitItem` — add context + interactivity

`src/LifeGrid.Presentation/ViewModels/WeeklyHabitItem.cs`

New constructor signature:
```csharp
public WeeklyHabitItem(WeeklyHabitItemDto dto, string goalDescription, string weekLabel, bool isInteractive)
```

New properties:
```csharp
public string GoalDescription { get; }
public string WeekLabel       { get; }
public bool   IsInteractive   { get; }
```

#### 4.2 `WeeklyGoalGroupItem` — propagate future flag

`src/LifeGrid.Presentation/ViewModels/WeeklyGoalGroupItem.cs`

Constructor gains `bool isFuture` parameter. Build each `WeeklyHabitItem` as:
```csharp
new WeeklyHabitItem(h, dto.GoalDescription, weekLabel, isInteractive: !isFuture)
```

Where `weekLabel` is the existing `WeekLabel` property.

#### 4.3 `WeeklyHabitsViewModel` — future flag + logging command

`src/LifeGrid.Presentation/ViewModels/WeeklyHabitsViewModel.cs`

In `LoadAsync`, after receiving `dto`:
```csharp
var isFuture = dto.StartDate.Date > DateTime.Today;
GoalGroups.Clear();
foreach (var g in dto.GoalGroups)
    GoalGroups.Add(new WeeklyGoalGroupItem(g, isFuture));
```

Add command:
```csharp
[RelayCommand]
private async Task OpenHabitLoggingAsync(WeeklyHabitItem item)
{
    if (!item.IsInteractive) return;
    await Shell.Current.GoToAsync("habit-logging", new ShellNavigationQueryParameters
    {
        ["habitId"]          = item.HabitId,
        ["habitName"]        = item.HabitName,
        ["habitDescription"] = item.HabitDescription,
        ["targetText"]       = item.TargetText,
        ["measurementUnit"]  = item.MeasurementUnit,
        ["goalDescription"]  = item.GoalDescription,
        ["weekLabel"]        = item.WeekLabel
    });
}
```

#### 4.4 `HomeViewModel` — logging command

`src/LifeGrid.Presentation/ViewModels/HomeViewModel.cs`

Same `OpenHabitLoggingAsync` command without the `IsInteractive` guard (Home shows current week only).

In `LoadAsync`, pass `isFuture: false` when building `WeeklyGoalGroupItem`.

#### 4.5 `HabitLoggingViewModel` (new)

`src/LifeGrid.Presentation/ViewModels/HabitLoggingViewModel.cs`

```csharp
public partial class HabitLoggingViewModel(IMediator mediator)
    : ObservableObject, IQueryAttributable
{
    [ObservableProperty] private Guid    _habitId;
    [ObservableProperty] private string  _habitName        = string.Empty;
    [ObservableProperty] private string  _habitDescription = string.Empty;
    [ObservableProperty] private string  _targetText       = string.Empty;
    [ObservableProperty] private string  _measurementUnit  = string.Empty;
    [ObservableProperty] private string  _goalDescription  = string.Empty;
    [ObservableProperty] private string  _weekLabel        = string.Empty;
    [ObservableProperty] private string  _actualValue      = string.Empty;
    [ObservableProperty] private string? _proofText;
    [ObservableProperty] private string? _proofImageUrl;
    [ObservableProperty] private string  _errorMessage     = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LogProgressCommand))]
    private bool _isBusy;

    public bool IsNotBusy => !_isBusy;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("habitId",          out var hid) && hid is Guid g) HabitId          = g;
        if (query.TryGetValue("habitName",         out var hn))  HabitName         = hn.ToString()!;
        if (query.TryGetValue("habitDescription",  out var hd))  HabitDescription  = hd.ToString()!;
        if (query.TryGetValue("targetText",        out var tt))  TargetText        = tt.ToString()!;
        if (query.TryGetValue("measurementUnit",   out var mu))  MeasurementUnit   = mu.ToString()!;
        if (query.TryGetValue("goalDescription",   out var gd))  GoalDescription   = gd.ToString()!;
        if (query.TryGetValue("weekLabel",         out var wl))  WeekLabel         = wl.ToString()!;
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task LogProgressAsync()
    {
        ErrorMessage = string.Empty;
        if (!double.TryParse(ActualValue, out var value) || value <= 0)
        {
            ErrorMessage = "Please enter a value greater than zero.";
            return;
        }
        IsBusy = true;
        var result = await mediator.Send(new LogHabitProgressCommand(
            HabitId, value, MeasurementUnit,
            string.IsNullOrWhiteSpace(ProofText) ? null : ProofText,
            ProofImageUrl));
        IsBusy = false;
        if (!result.IsSuccess) { ErrorMessage = result.Error ?? "Failed to log progress."; return; }
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task PickProofImageAsync()
    {
        var picked = await FilePicker.Default.PickAsync(
            new PickOptions { FileTypes = FilePickerFileType.Images, PickerTitle = "Select proof image" });
        if (picked is not null) ProofImageUrl = picked.FullPath;
    }
}
```

---

### Phase 5 — Presentation: Pages (`MAUI_UX_ENGINEER`)

**5 files: 2 new, 3 modified.**

#### 5.1 `HabitLoggingPage.xaml` (new)

`src/LifeGrid.Presentation/Pages/HabitLoggingPage.xaml`

Grid with `RowDefinitions="Auto,*,Auto"` (Zone A header, Zone B scrollable form, Zone C action bar):

**Zone A — Context Header (read-only, `BackgroundColor="{StaticResource Surface}"`, `Padding="16,12"`):**
```xml
<VerticalStackLayout Spacing="2">
    <Label Text="{Binding GoalDescription}"  FontFamily="{StaticResource FontDMMono}"       FontSize="13" FontAttributes="Bold" />
    <Label Text="{Binding WeekLabel}"        FontFamily="{StaticResource FontShareTechMono}" FontSize="11" Opacity="0.55" />
    <Label Text="{Binding HabitName}"        FontFamily="{StaticResource FontDMMono}"       FontSize="16" FontAttributes="Bold" Margin="0,6,0,0" />
    <Label Text="{Binding HabitDescription}" FontFamily="{StaticResource FontShareTechMono}" FontSize="12" LineBreakMode="WordWrap" />
    <Label Text="{Binding TargetText}"       FontFamily="{StaticResource FontShareTechMono}" FontSize="11" Opacity="0.55" />
</VerticalStackLayout>
```

**Zone B — Form (ScrollView, `Padding="16,12"`):**
- `ActualValue` row: `Grid ColumnDefinitions="*,Auto"` containing a `Border`-wrapped `Entry` (keyboard `Numeric`) + muted `Label` for `MeasurementUnit`
- `ProofText` `Editor` (multi-line, `Placeholder="Describe your proof (optional)"`, Share Tech Mono, 2px corner border)
- `Button` "Attach Image" (Share Tech Mono, Secondary text, 2px radius) + adjacent `Label` showing file name when `ProofImageUrl` is non-null

**Zone C — Action (`Padding="16,8"`):**
- `Label` `ErrorMessage` (Error color `#FFFF1B77`, hidden via `IsVisible="{Binding ErrorMessage, Converter={StaticResource StringNotEmptyConverter}}"`)
- `Button` "Log Progress" (Primary style, 2px radius, `IsEnabled="{Binding IsNotBusy}"`)

#### 5.2 `HabitLoggingPage.xaml.cs` (new)

```csharp
public partial class HabitLoggingPage : ContentPage
{
    public HabitLoggingPage(HabitLoggingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
```

#### 5.3 `WeeklyHabitsPage.xaml` — tap gesture + future lock

Add inside the habit card `Border` (alongside existing `VerticalStackLayout`):

```xml
<Border.GestureRecognizers>
    <TapGestureRecognizer
        Command="{Binding BindingContext.OpenHabitLoggingCommand, Source={x:Reference PageRoot}}"
        CommandParameter="{Binding .}" />
</Border.GestureRecognizers>
<Border.Triggers>
    <DataTrigger TargetType="Border" Binding="{Binding IsInteractive}" Value="False">
        <Setter Property="Opacity" Value="0.4" />
    </DataTrigger>
</Border.Triggers>
```

#### 5.4 `HomePage.xaml` — tap gesture

Same tap gesture on the habit card `Border` (no opacity trigger needed):

```xml
<Border.GestureRecognizers>
    <TapGestureRecognizer
        Command="{Binding BindingContext.OpenHabitLoggingCommand, Source={x:Reference PageRoot}}"
        CommandParameter="{Binding .}" />
</Border.GestureRecognizers>
```

#### 5.5 DI & Routing

`src/LifeGrid.Presentation/MauiProgram.cs`:
```csharp
builder.Services.AddTransient<HabitLoggingViewModel>();
builder.Services.AddTransient<HabitLoggingPage>();
```

`src/LifeGrid.Presentation/AppShell.xaml.cs` (in constructor, alongside existing route registrations):
```csharp
Routing.RegisterRoute("habit-logging", typeof(HabitLoggingPage));
```

---

### Phase 6 — Tests (`TDD_SPECIALIST`)

**1 file new (Application), 1 file modified (Infrastructure).**

#### 6.1 Application Tests

**New file:** `tests/LifeGrid.Application.Tests/HabitLogging/LogHabitProgressCommandTests.cs`

Dependencies mocked: `IHabitRepository`, `IDateTimeProvider`, `IUnitOfWork` via NSubstitute.

| Test | Description |
|---|---|
| `LogHabitProgress_ValueZero_ReturnsFailure` | `ActualValue = 0`; asserts `result.IsSuccess == false`; `CommitAsync` never called |
| `LogHabitProgress_NegativeValue_ReturnsFailure` | `ActualValue = -1.5`; same assertions |
| `LogHabitProgress_HabitNotFound_ReturnsFailure` | `GetByIdAsync` returns `null`; asserts `result.IsSuccess == false`; `CommitAsync` never called |
| `LogHabitProgress_HappyPath_AddsLogAndCommits` | Valid habit exists; asserts `AddCompletionLogAsync` called once with log whose `HabitId` matches; `CommitAsync` called once; `result.IsSuccess == true` |
| `LogHabitProgress_HappyPath_UsesDateTimeProviderTimestamp` | Mock `IDateTimeProvider.UtcNow` returns fixed `DateTime`; assert persisted log's `Timestamp` equals that value |

#### 6.2 Infrastructure Integration Tests

Add two tests to `tests/LifeGrid.Infrastructure.Tests/Data/HabitRepositoryTests.cs`:

| Test | Description |
|---|---|
| `AddCompletionLogAsync_IncreasesCount` | Seed habit; call `AddCompletionLogAsync` + `SaveChanges`; assert `db.CompletedValueLogs.Count() == 1` |
| `AddCompletionLogAsync_PersistsCorrectTimestamp` | Create log with fixed `DateTime`; persist; re-query; assert `Timestamp` matches |

---

## File Change Summary

| File | Change |
|---|---|
| `src/LifeGrid.Domain/Habit/CompletedValueLog.cs` | Modified — `Create` accepts `DateTime timestamp` |
| `src/LifeGrid.Application/Habit/IHabitRepository.cs` | Modified — add `GetByIdAsync`, `AddCompletionLogAsync` |
| `src/LifeGrid.Application/HabitLogging/LogHabitProgressCommand.cs` | **New** |
| `src/LifeGrid.Application/HabitLogging/LogHabitProgressCommandHandler.cs` | **New** |
| `src/LifeGrid.Infrastructure/Data/Repositories/HabitRepository.cs` | Modified — implement 2 new methods |
| `src/LifeGrid.Presentation/ViewModels/WeeklyHabitItem.cs` | Modified — add `GoalDescription`, `WeekLabel`, `IsInteractive` |
| `src/LifeGrid.Presentation/ViewModels/WeeklyGoalGroupItem.cs` | Modified — propagate `isFuture` to habit items |
| `src/LifeGrid.Presentation/ViewModels/WeeklyHabitsViewModel.cs` | Modified — future flag + `OpenHabitLoggingCommand` |
| `src/LifeGrid.Presentation/ViewModels/HomeViewModel.cs` | Modified — `OpenHabitLoggingCommand` + pass `isFuture: false` |
| `src/LifeGrid.Presentation/ViewModels/HabitLoggingViewModel.cs` | **New** |
| `src/LifeGrid.Presentation/Pages/HabitLoggingPage.xaml` | **New** |
| `src/LifeGrid.Presentation/Pages/HabitLoggingPage.xaml.cs` | **New** |
| `src/LifeGrid.Presentation/Pages/WeeklyHabitsPage.xaml` | Modified — tap gesture + opacity trigger |
| `src/LifeGrid.Presentation/Pages/HomePage.xaml` | Modified — tap gesture |
| `src/LifeGrid.Presentation/MauiProgram.cs` | Modified — register VM + Page |
| `src/LifeGrid.Presentation/AppShell.xaml.cs` | Modified — register `"habit-logging"` route |
| `tests/LifeGrid.Application.Tests/HabitLogging/LogHabitProgressCommandTests.cs` | **New** — 5 tests |
| `tests/LifeGrid.Infrastructure.Tests/Data/HabitRepositoryTests.cs` | Modified — 2 new tests |

**Total: 18 files. 7 new tests. Expected: 264 → 271 passing.**

---

## Acceptance Criteria

- `dotnet build LifeGrid.slnx` → 0 errors, 0 warnings.
- `dotnet test` → 271 tests pass.
- Tapping a habit card on Home or Week Detail → `HabitLoggingPage` pushes with correct context header.
- Value ≤ 0 + "Log Progress" → inline error, no navigation.
- Valid value + "Log Progress" → modal pops, parent refreshes, new log entry visible in habit card.
- Proof text and proof image each accepted independently; no XOR enforcement.
- Future-week habit cards in `WeeklyHabitsPage` at 40% opacity, tap does nothing.
- Home habit cards at full opacity, all interactive.
