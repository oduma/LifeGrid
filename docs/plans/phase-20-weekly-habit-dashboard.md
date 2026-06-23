# Phase 20 — Weekly Habit Dashboard & Filtered Drill-down
**Status:** DONE (implemented 2026-06-23)

## Source Documents
- Requirements: `docs/requirements/FUNCTIONAL_REQUIREMENTS.md §P20`
- Phase 20 spec: `docs/requirements/Phase-20-requirements.md`
- Wireframe: `docs/specs/assets/wireframes/wf8.png`

---

## Clarifications Confirmed (2026-06-22)

| Question | Answer |
|---|---|
| `CompletedValuesLog` — add to domain or skip? | Add it: new entity, EF migration, new table |
| Timeline tap behaviour | Keep `SelectWeekCommand` (highlight only); add a "VIEW WEEK DETAIL" button inside the selected card |
| Layout — flat list vs nested grouping | Nested (Zone B goal sub-header + Zone C habit cards), as written in requirements text |
| TDD scope for ViewModel navigation test | Application-layer unit tests only; ViewModel navigation is manual |
| `HabitType` in `Habit.Create()` | Make it a required parameter (future-safe); all current callers pass `HabitType.Planned` |

---

## Post-Implementation Corrections

| Area | Plan | Actual / Fix |
|---|---|---|
| `GetWeeklyHabitsQueryHandler` accessibility | `internal sealed class` | Changed to `public sealed class` — tests instantiate it directly; matches all other handlers in the project |
| Proof text visibility | `NullToBoolConverter.Instance` in XAML | Converter doesn't exist; added `HasProofText` computed bool to `HabitCompletionLogItem` and bound `IsVisible` directly |
| EF migration `--startup-project` | `src/LifeGrid.Presentation` | Must be `src/LifeGrid.Infrastructure` — MAUI project multi-targets `net10.0-android` and fails at design time; `LifeGridDbContextFactory` in Infrastructure is the correct entry point |

---

## Architecture Overview

### New Types

| Type | Layer | Purpose |
|---|---|---|
| `CompletedValueLog` | Domain | Completion log entry owned by `Habit` aggregate |
| `GetWeeklyHabitsQuery` | Application | Query carrying `WeekId` + optional `FilterGoalIds` |
| `WeeklyHabitsDashboardDto` (+ 3 child DTOs) | Application | Full nested DTO tree for the weekly view |
| `GetWeeklyHabitsQueryHandler` | Application | Builds the DTO: loads week, filters goals, batch-loads habits |
| `WeeklyGoalGroupItem` | Presentation | Observable item for a goal group in the list |
| `WeeklyHabitItem` | Presentation | Observable item for a single habit card |
| `HabitCompletionLogItem` | Presentation | Immutable record for a completion log row |
| `WeeklyHabitsViewModel` | Presentation | `IQueryAttributable`; owns `GoalGroups` collection + proof image overlay state |
| `WeeklyHabitsPage` | Presentation | Pushed page; Zone A header + scrollable Zone B/C + proof image overlay |

### Modified Types

| Type | Change |
|---|---|
| `Habit` (Domain) | Add `_completedValuesLog`/`CompletedValuesLog`; update `Create()` to take `HabitType` parameter |
| `HabitConfiguration` | Add navigation to `CompletedValueLog` |
| `LifeGridDbContext` | Add `CompletedValueLogs` DbSet |
| `IHabitRepository` | Add `GetByWeekGoalIdsAsync` (batch, with completion logs included) |
| `HabitRepository` | Implement `GetByWeekGoalIdsAsync` |
| `IWeekRepository` | Add `GetByIdAsync(Guid weekId)` |
| `WeekRepository` | Implement `GetByIdAsync` (includes WeekGoals) |
| `GenerateScheduleCommand` | Pass `HabitType.Planned` to updated `Habit.Create()` |
| `RecalculateGoalScheduleCommand` | Pass `HabitType.Planned` to updated `Habit.Create()` |
| `HabitEntityTests` | Pass `HabitType.Planned` to updated `Habit.Create()` |
| `HabitRepositoryTests` | Pass `HabitType.Planned` to updated `Habit.Create()` |
| `FactoryResetServiceTests` | Pass `HabitType.Planned` to updated `Habit.Create()` |
| `TimelineViewModel` | Add `DrillDownToWeekCommand` |
| `TimelinePage.xaml` | Add "VIEW WEEK DETAIL" button (visible when card `IsSelected`) |
| `AppShell.xaml.cs` | Register `"week-detail"` route |
| `MauiProgram.cs` | Register `WeeklyHabitsViewModel` + `WeeklyHabitsPage` as Transient |

### Key Design Decisions

1. **`CompletedValueLog` as a first-class entity** — it has its own PK (`LogId`) and lives in a separate `HabitCompletionLogs` table with a cascading FK to `Habits`. Modelled as a class (not value object) because it is independently identifiable.
2. **Batch habit load** — `GetByWeekGoalIdsAsync` loads all habits for the week in one DB round-trip using `.Where(h => weekGoalIds.Contains(h.WeekGoalId)).Include(h => h.CompletedValuesLog)`.
3. **Proof image overlay is in-page** — no new NuGet packages; a full-page `Grid` overlay with `IsVisible` binding handles proof image expansion without requiring a popup library.
4. **`HabitType` made a required `Create()` parameter** — all current callers pass `HabitType.Planned`; the domain is now ready for future types without further changes.
5. **Navigation passes typed objects** — `weekId` as `Guid`, `filterGoalIds` as `IReadOnlyList<Guid>?` via `ShellNavigationQueryParameters` (same pattern as Phase 19).

---

## Implementation Plan

### Phase A — TDD Stubs (Write Failing Tests First)

**File:** `tests/LifeGrid.Application.Tests/WeeklyHabits/GetWeeklyHabitsQueryTests.cs` (new)

Three tests, all failing until Phase D:

| Test name | What it asserts |
|---|---|
| `NullFilter_ReturnsAllGoalGroups` | Week has 2 WeekGoals; `FilterGoalIds = null` → dashboard has 2 goal groups |
| `FilteredGoalIds_ExcludesNonMatchingGoalGroups` | Week has goalA + goalB; filter `[goalA]` → only goalA group returned |
| `HabitsGroupedUnderCorrectWeekGoal` | goalA has 1 habit, goalB has 2 habits; unfiltered → habits correctly nested under each group |

**Setup pattern:**
- Mock `IWeekRepository.GetByIdAsync` returning a `Week` with `WeekGoals`.
- Mock `IGoalRepository.GetByIdsAsync` returning matching `Goal` aggregates.
- Mock `IHabitRepository.GetByWeekGoalIdsAsync` returning `Habit` entities (with empty `CompletedValuesLog`).
- Assert on the returned `WeeklyHabitsDashboardDto.GoalGroups` shape.

> **Note:** `Habit.Create()` now requires a `HabitType` parameter — pass `HabitType.Planned` in all test helper calls.

---

### Phase B — Domain: CompletedValueLog + Habit Update

**B1. New file:** `src/LifeGrid.Domain/Habit/CompletedValueLog.cs`

```csharp
namespace LifeGrid.Domain.Habit;

public sealed class CompletedValueLog
{
    private CompletedValueLog() { }

    public static CompletedValueLog Create(
        Guid     habitId,
        double   actualValue,
        string   measurementUnit,
        string?  proofText,
        string?  proofImageUrl) => new()
    {
        LogId           = Guid.NewGuid(),
        HabitId         = habitId,
        ActualValue     = actualValue,
        MeasurementUnit = measurementUnit,
        ProofText       = proofText,
        ProofImageUrl   = proofImageUrl,
        Timestamp       = DateTime.UtcNow
    };

    public Guid     LogId           { get; private set; }
    public Guid     HabitId         { get; private set; }
    public double   ActualValue     { get; private set; }
    public string   MeasurementUnit { get; private set; } = string.Empty;
    public string?  ProofText       { get; private set; }
    public string?  ProofImageUrl   { get; private set; }
    public DateTime Timestamp       { get; private set; }
}
```

**B2. Update:** `src/LifeGrid.Domain/Habit/Habit.cs`

- Add `private readonly List<CompletedValueLog> _completedValuesLog = new();`
- Add `public IReadOnlyCollection<CompletedValueLog> CompletedValuesLog => _completedValuesLog.AsReadOnly();`
- Add `internal void AddCompletionLog(CompletedValueLog log) => _completedValuesLog.Add(log);`
- Update `Create()` signature to accept `HabitType habitType` as the **second parameter** (after `weekGoalId`); remove the hardcoded `HabitType = HabitType.Planned` assignment.

New `Create()` signature:
```csharp
public static Habit Create(
    Guid      weekGoalId,
    HabitType habitType,
    string    habitName,
    string    habitDescription,
    double    targetValue,
    string    measurementUnit,
    DateTime  deadlineDateTime)
```

**B3. Update all callers** (5 files — see Modified Types table):
Each call site changes from `Habit.Create(weekGoalId, name, desc, ...)` to `Habit.Create(weekGoalId, HabitType.Planned, name, desc, ...)`.

---

### Phase C — Infrastructure: EF Config, Migration, Repositories

**C1. New file:** `src/LifeGrid.Infrastructure/Data/EntityConfigurations/CompletedValueLogConfiguration.cs`

```csharp
using LifeGrid.Domain.Habit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class CompletedValueLogConfiguration : IEntityTypeConfiguration<CompletedValueLog>
{
    public void Configure(EntityTypeBuilder<CompletedValueLog> builder)
    {
        builder.ToTable("HabitCompletionLogs");
        builder.HasKey(l => l.LogId);
        builder.Property(l => l.HabitId);
        builder.Property(l => l.ActualValue);
        builder.Property(l => l.MeasurementUnit).HasMaxLength(100);
        builder.Property(l => l.ProofText).HasMaxLength(2000);
        builder.Property(l => l.ProofImageUrl).HasMaxLength(1000);
        builder.Property(l => l.Timestamp);
    }
}
```

**C2. Update:** `src/LifeGrid.Infrastructure/Data/EntityConfigurations/HabitConfiguration.cs`

Add at the end of `Configure`:
```csharp
builder.HasMany(h => h.CompletedValuesLog)
       .WithOne()
       .HasForeignKey(l => l.HabitId)
       .OnDelete(DeleteBehavior.Cascade);
```

**C3. Update:** `src/LifeGrid.Infrastructure/Data/LifeGridDbContext.cs`

Add:
```csharp
public DbSet<CompletedValueLog> CompletedValueLogs => Set<CompletedValueLog>();
```

**C4. Update:** `src/LifeGrid.Application/Habit/IHabitRepository.cs`

Add:
```csharp
Task<IReadOnlyList<HabitEntity>> GetByWeekGoalIdsAsync(
    IReadOnlyList<Guid> weekGoalIds, CancellationToken ct = default);
```

**Update:** `src/LifeGrid.Infrastructure/Data/Repositories/HabitRepository.cs`

Add implementation:
```csharp
public async Task<IReadOnlyList<HabitEntity>> GetByWeekGoalIdsAsync(
    IReadOnlyList<Guid> weekGoalIds, CancellationToken ct = default)
    => await db.Habits
        .Include(h => h.CompletedValuesLog)
        .Where(h => weekGoalIds.Contains(h.WeekGoalId))
        .ToListAsync(ct);
```

**C5. Update:** `src/LifeGrid.Application/Week/IWeekRepository.cs`

Add:
```csharp
Task<WeekEntity?> GetByIdAsync(Guid weekId, CancellationToken ct = default);
```

**Update:** `src/LifeGrid.Infrastructure/Data/Repositories/WeekRepository.cs`

Add implementation:
```csharp
public async Task<WeekEntity?> GetByIdAsync(Guid weekId, CancellationToken ct = default)
    => await db.Weeks
        .Include(w => w.WeekGoals)
        .FirstOrDefaultAsync(w => w.WeekId == weekId, ct);
```

**C6. EF Migration**

Run:
```
dotnet ef migrations add Phase20_AddHabitCompletionLogs --project src/LifeGrid.Infrastructure --startup-project src/LifeGrid.Presentation
```

Verify the generated migration creates `HabitCompletionLogs` with the correct FK and cascade delete.

---

### Phase D — Application: DTOs + Query + Handler

**New folder:** `src/LifeGrid.Application/WeeklyHabits/`

**D1. DTOs** — `WeeklyHabitsDashboardDto.cs`:

```csharp
namespace LifeGrid.Application.WeeklyHabits;

public record WeeklyHabitsDashboardDto(
    Guid   WeekId,
    DateTime StartDate,
    string   Status,
    int      TotalWeeklySpEarned,
    IReadOnlyList<WeeklyGoalGroupDto> GoalGroups);

public record WeeklyGoalGroupDto(
    Guid   GoalId,
    string GoalDescription,
    int    WeekGoalNumber,
    string PenaltyState,
    double GoalWeeklyGp,
    int    GoalWeeklyXpEarned,
    IReadOnlyList<WeeklyHabitItemDto> Habits);

public record WeeklyHabitItemDto(
    Guid     HabitId,
    string   HabitType,
    string   HabitName,
    string   HabitDescription,
    double   TargetValue,
    string   MeasurementUnit,
    DateTime DeadlineDateTime,
    IReadOnlyList<HabitCompletionLogDto> CompletionLogs);

public record HabitCompletionLogDto(
    Guid     LogId,
    double   ActualValue,
    string   MeasurementUnit,
    string?  ProofText,
    string?  ProofImageUrl,
    DateTime Timestamp);
```

**D2. Query** — `GetWeeklyHabitsQuery.cs`:

```csharp
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.WeeklyHabits;

public record GetWeeklyHabitsQuery(
    Guid                  WeekId,
    IReadOnlyList<Guid>?  FilterGoalIds = null)
    : IRequest<Result<WeeklyHabitsDashboardDto>>;
```

**D3. Handler** — `GetWeeklyHabitsQueryHandler.cs`:

```csharp
using HabitEntity = LifeGrid.Domain.Habit.Habit;
using MediatR;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Habit;
using LifeGrid.Application.Week;
using LifeGrid.Domain.Common;

namespace LifeGrid.Application.WeeklyHabits;

internal sealed class GetWeeklyHabitsQueryHandler(
    IWeekRepository   weekRepository,
    IGoalRepository   goalRepository,
    IHabitRepository  habitRepository)
    : IRequestHandler<GetWeeklyHabitsQuery, Result<WeeklyHabitsDashboardDto>>
{
    public async Task<Result<WeeklyHabitsDashboardDto>> Handle(
        GetWeeklyHabitsQuery   request,
        CancellationToken      cancellationToken)
    {
        var week = await weekRepository.GetByIdAsync(request.WeekId, cancellationToken);
        if (week is null)
            return Result<WeeklyHabitsDashboardDto>.Failure("Week not found.");

        // Apply optional goal filter
        HashSet<Guid>? filterSet = request.FilterGoalIds is { Count: > 0 } f
            ? f.ToHashSet() : null;

        var weekGoals = (filterSet is null
            ? week.WeekGoals
            : week.WeekGoals.Where(wg => filterSet.Contains(wg.GoalId)))
            .ToList();

        // Batch-load goal descriptions
        var goalIds = weekGoals.Select(wg => wg.GoalId).Distinct().ToList();
        var goals   = await goalRepository.GetByIdsAsync(goalIds, cancellationToken);
        var descMap = goals.ToDictionary(g => g.GoalId, g => g.Description);

        // Batch-load habits with completion logs
        var weekGoalIds    = weekGoals.Select(wg => wg.WeekGoalId).ToList();
        var habits         = await habitRepository.GetByWeekGoalIdsAsync(weekGoalIds, cancellationToken);
        var habitsByWgId   = habits.GroupBy(h => h.WeekGoalId)
                                   .ToDictionary(g => g.Key, g => g.ToList());

        var groups = weekGoals.Select(wg =>
        {
            var wgHabits = habitsByWgId.TryGetValue(wg.WeekGoalId, out var list)
                ? list : new List<HabitEntity>();

            return new WeeklyGoalGroupDto(
                wg.GoalId,
                descMap.GetValueOrDefault(wg.GoalId, string.Empty),
                wg.WeekGoalNumber,
                wg.PenaltyState.ToString(),
                wg.GoalWeeklyGp,
                wg.GoalWeeklyXpEarned,
                wgHabits.Select(h => new WeeklyHabitItemDto(
                    h.HabitId,
                    h.HabitType.ToString(),
                    h.HabitName,
                    h.HabitDescription,
                    h.TargetValue,
                    h.MeasurementUnit,
                    h.DeadlineDateTime,
                    h.CompletedValuesLog.Select(l => new HabitCompletionLogDto(
                        l.LogId,
                        l.ActualValue,
                        l.MeasurementUnit,
                        l.ProofText,
                        l.ProofImageUrl,
                        l.Timestamp)).ToList()
                )).ToList());
        }).ToList();

        return Result<WeeklyHabitsDashboardDto>.Success(new WeeklyHabitsDashboardDto(
            week.WeekId,
            week.StartDate,
            week.Status.ToString(),
            week.TotalWeeklySpEarned,
            groups));
    }
}
```

---

### Phase E — Presentation: ViewModels & Items

**New folder:** `src/LifeGrid.Presentation/ViewModels/`

**E1. HabitCompletionLogItem.cs**

```csharp
namespace LifeGrid.Presentation.ViewModels;

public sealed record HabitCompletionLogItem(
    Guid     LogId,
    double   ActualValue,
    string   MeasurementUnit,
    string?  ProofText,
    string?  ProofImageUrl,
    DateTime Timestamp)
{
    public bool   HasProofImage => !string.IsNullOrWhiteSpace(ProofImageUrl);
    public string LogSummary    => $"{ActualValue} {MeasurementUnit} @ {Timestamp:HH:mm}";
    public string? ProofTextDisplay => string.IsNullOrWhiteSpace(ProofText) ? null : ProofText;
}
```

**E2. WeeklyHabitItem.cs**

```csharp
using LifeGrid.Application.WeeklyHabits;

namespace LifeGrid.Presentation.ViewModels;

public sealed class WeeklyHabitItem
{
    public WeeklyHabitItem(WeeklyHabitItemDto dto)
    {
        HabitId          = dto.HabitId;
        HabitTypeLabel   = dto.HabitType;       // "Planned", "MomentBurst", "Flash"
        HabitName        = dto.HabitName;
        HabitDescription = dto.HabitDescription;
        TargetText       = $"{dto.TargetValue} {dto.MeasurementUnit} by {dto.DeadlineDateTime:MMM dd}";
        CompletionLogs   = dto.CompletionLogs
            .Select(l => new HabitCompletionLogItem(
                l.LogId, l.ActualValue, l.MeasurementUnit,
                l.ProofText, l.ProofImageUrl, l.Timestamp))
            .ToList();
    }

    public Guid     HabitId          { get; }
    public string   HabitTypeLabel   { get; }
    public string   HabitName        { get; }
    public string   HabitDescription { get; }
    public string   TargetText       { get; }
    public IReadOnlyList<HabitCompletionLogItem> CompletionLogs { get; }
}
```

**E3. WeeklyGoalGroupItem.cs**

```csharp
using LifeGrid.Application.WeeklyHabits;
using LifeGrid.Domain.WeekGoal;

namespace LifeGrid.Presentation.ViewModels;

public sealed class WeeklyGoalGroupItem
{
    public WeeklyGoalGroupItem(WeeklyGoalGroupDto dto)
    {
        GoalDescription    = dto.GoalDescription;
        WeekLabel          = $"Week {dto.WeekGoalNumber}";
        PenaltyState       = dto.PenaltyState;
        GoalWeeklyGp       = dto.GoalWeeklyGp;
        GoalWeeklyXpEarned = dto.GoalWeeklyXpEarned;
        IsInPenalty        = dto.PenaltyState is
            nameof(PenaltyState.Probation_Week_2) or nameof(PenaltyState.Reckoning_Week_3);
        MetricsText        = $"GP: {dto.GoalWeeklyGp:F2}  XP: {dto.GoalWeeklyXpEarned}";
        Habits             = dto.Habits.Select(h => new WeeklyHabitItem(h)).ToList();
    }

    public string GoalDescription    { get; }
    public string WeekLabel          { get; }
    public string PenaltyState       { get; }
    public double GoalWeeklyGp       { get; }
    public int    GoalWeeklyXpEarned { get; }
    public bool   IsInPenalty        { get; }
    public string MetricsText        { get; }
    public IReadOnlyList<WeeklyHabitItem> Habits { get; }
}
```

**E4. WeeklyHabitsViewModel.cs**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeGrid.Application.WeeklyHabits;
using MediatR;
using System.Collections.ObjectModel;

namespace LifeGrid.Presentation.ViewModels;

public partial class WeeklyHabitsViewModel(IMediator mediator)
    : ObservableObject, IQueryAttributable
{
    private Guid                  _weekId;
    private IReadOnlyList<Guid>?  _filterGoalIds;

    [ObservableProperty] private string _weekHeaderText          = string.Empty;
    [ObservableProperty] private string _weekStatusText          = string.Empty;
    [ObservableProperty] private int    _totalSp;
    [ObservableProperty] private string? _proofImageUrl;
    [ObservableProperty] private bool   _isProofImageOverlayVisible;

    public ObservableCollection<WeeklyGoalGroupItem> GoalGroups { get; } = new();

    // Shell fires ApplyQueryAttributes AFTER OnAppearing for tab nav, but WeeklyHabitsPage
    // is a pushed page so the order is reversed — ApplyQueryAttributes fires first.
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("weekId", out var wid) && wid is Guid weekId)
            _weekId = weekId;

        _filterGoalIds = query.TryGetValue("filterGoalIds", out var fids) &&
                         fids is IReadOnlyList<Guid> { Count: > 0 } ids
            ? ids : null;
    }

    public async Task LoadAsync()
    {
        var result = await mediator.Send(new GetWeeklyHabitsQuery(_weekId, _filterGoalIds));
        if (!result.IsSuccess) return;

        var dto        = result.Value!;
        WeekHeaderText = dto.StartDate.ToString("MMM dd, yyyy");
        WeekStatusText = $"{dto.Status} | SP: {dto.TotalWeeklySpEarned}";
        TotalSp        = dto.TotalWeeklySpEarned;

        GoalGroups.Clear();
        foreach (var g in dto.GoalGroups)
            GoalGroups.Add(new WeeklyGoalGroupItem(g));
    }

    [RelayCommand]
    private void ShowProofImage(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        ProofImageUrl              = url;
        IsProofImageOverlayVisible = true;
    }

    [RelayCommand]
    private void DismissProofImage()
    {
        IsProofImageOverlayVisible = false;
        ProofImageUrl              = null;
    }
}
```

---

### Phase F — WeeklyHabitsPage.xaml + .xaml.cs

**New file:** `src/LifeGrid.Presentation/Pages/WeeklyHabitsPage.xaml`

Layout skeleton:

```xml
<ContentPage
    xmlns="..."
    x:Class="LifeGrid.Presentation.Pages.WeeklyHabitsPage"
    x:Name="PageRoot"
    Title="Week Detail"
    BackgroundColor="{StaticResource Background}">

    <Grid RowDefinitions="Auto,*">

        <!-- Zone A: Global week header (fixed) -->
        <Border Grid.Row="0" Margin="16,8" Padding="12" ...>
            <Grid ColumnDefinitions="*,Auto">
                <Label Text="{Binding WeekHeaderText}" ... />
                <Label Text="{Binding WeekStatusText}" ... />
            </Grid>
        </Border>

        <!-- Zone B+C: Scrollable goal groups + habit cards -->
        <ScrollView Grid.Row="1">
            <VerticalStackLayout
                BindableLayout.ItemsSource="{Binding GoalGroups}"
                Spacing="16"
                Padding="16,8">

                <BindableLayout.ItemTemplate>
                    <DataTemplate x:DataType="vm:WeeklyGoalGroupItem">
                        <VerticalStackLayout Spacing="8">

                            <!-- Goal sub-header -->
                            <Grid ColumnDefinitions="*,Auto">
                                <Label Text="{Binding GoalDescription}" FontSize="16" FontAttributes="Bold" ... />
                                <Label Text="{Binding WeekLabel}" Grid.Column="1" ... />
                            </Grid>

                            <!-- Goal metrics + penalty indicator -->
                            <Label Text="{Binding MetricsText}" ...>
                                <Label.Triggers>
                                    <DataTrigger TargetType="Label" Binding="{Binding IsInPenalty}" Value="True">
                                        <Setter Property="TextColor" Value="{StaticResource Warning}" />
                                    </DataTrigger>
                                </Label.Triggers>
                            </Label>

                            <!-- Zone C: Habit cards -->
                            <VerticalStackLayout
                                BindableLayout.ItemsSource="{Binding Habits}"
                                Spacing="8">
                                <BindableLayout.ItemTemplate>
                                    <DataTemplate x:DataType="vm:WeeklyHabitItem">

                                        <Border Padding="12" StrokeShape="RoundRectangle 2" ...>
                                            <VerticalStackLayout Spacing="6">

                                                <!-- Header: type label + name -->
                                                <Grid ColumnDefinitions="Auto,*">
                                                    <Label Text="{Binding HabitTypeLabel}"
                                                           FontSize="10" Opacity="0.6" ... />
                                                    <Label Grid.Column="1"
                                                           Text="{Binding HabitName}"
                                                           FontSize="14" FontAttributes="Bold" ... />
                                                </Grid>

                                                <!-- Description -->
                                                <Label Text="{Binding HabitDescription}" FontSize="12" ... />

                                                <!-- Target -->
                                                <Label Text="{Binding TargetText}"
                                                       FontSize="11" Opacity="0.6" ... />

                                                <!-- Completion log sub-list -->
                                                <VerticalStackLayout
                                                    BindableLayout.ItemsSource="{Binding CompletionLogs}"
                                                    Spacing="4">
                                                    <BindableLayout.ItemTemplate>
                                                        <DataTemplate x:DataType="vm:HabitCompletionLogItem">
                                                            <Grid ColumnDefinitions="*,Auto">
                                                                <Label Text="{Binding LogSummary}" FontSize="11" ... />
                                                                <!-- Proof image icon — only when HasProofImage -->
                                                                <Label
                                                                    Grid.Column="1"
                                                                    Text="&#xE3F4;"
                                                                    FontFamily="MaterialSymbolsRounded"
                                                                    FontSize="18"
                                                                    IsVisible="{Binding HasProofImage}">
                                                                    <Label.GestureRecognizers>
                                                                        <TapGestureRecognizer
                                                                            Command="{Binding BindingContext.ShowProofImageCommand,
                                                                                      Source={x:Reference PageRoot}}"
                                                                            CommandParameter="{Binding ProofImageUrl}" />
                                                                    </Label.GestureRecognizers>
                                                                </Label>
                                                            </Grid>
                                                        </DataTemplate>
                                                    </BindableLayout.ItemTemplate>
                                                </VerticalStackLayout>

                                            </VerticalStackLayout>
                                        </Border>

                                    </DataTemplate>
                                </BindableLayout.ItemTemplate>
                            </VerticalStackLayout>

                        </VerticalStackLayout>
                    </DataTemplate>
                </BindableLayout.ItemTemplate>

            </VerticalStackLayout>
        </ScrollView>

        <!-- Proof image overlay — full-page, above all rows -->
        <Grid Grid.RowSpan="2"
              IsVisible="{Binding IsProofImageOverlayVisible}"
              BackgroundColor="#CC000000">
            <Image Source="{Binding ProofImageUrl}" Aspect="AspectFit" Margin="24" />
            <Button Text="×"
                    Command="{Binding DismissProofImageCommand}"
                    HorizontalOptions="End"
                    VerticalOptions="Start"
                    Margin="16" />
        </Grid>

    </Grid>
</ContentPage>
```

**New file:** `src/LifeGrid.Presentation/Pages/WeeklyHabitsPage.xaml.cs`

```csharp
using LifeGrid.Presentation.ViewModels;

namespace LifeGrid.Presentation.Pages;

public partial class WeeklyHabitsPage : ContentPage
{
    private readonly WeeklyHabitsViewModel _viewModel;

    public WeeklyHabitsPage(WeeklyHabitsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel     = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
```

> **Note:** For pushed pages, `ApplyQueryAttributes` fires **before** `OnAppearing`, so `LoadAsync()` in `OnAppearing` correctly uses the already-set `_weekId` and `_filterGoalIds`.

---

### Phase G — Timeline: Drill-Down Button

**G1. Update:** `src/LifeGrid.Presentation/ViewModels/TimelineViewModel.cs`

Add command:
```csharp
[RelayCommand]
private async Task DrillDownToWeekAsync(TimelineWeekItem item)
{
    var parameters = new ShellNavigationQueryParameters
    {
        ["weekId"] = item.WeekId
    };
    if (_filterGoalIds is { Count: > 0 })
        parameters["filterGoalIds"] = _filterGoalIds;

    await Shell.Current.GoToAsync("week-detail", parameters);
}
```

**G2. Update:** `src/LifeGrid.Presentation/Pages/TimelinePage.xaml`

Inside the week card `Border`, at the end of the `VerticalStackLayout` (after Zone B goals list), add:

```xml
<Button
    Text="VIEW WEEK DETAIL"
    IsVisible="{Binding IsSelected}"
    Command="{Binding Source={x:Reference PageRoot}, Path=BindingContext.DrillDownToWeekCommand}"
    CommandParameter="{Binding .}"
    Margin="0,8,0,0"
    FontFamily="{StaticResource FontShareTechMono}"
    FontSize="11" />
```

---

### Phase H — Registration

**H1. Update:** `src/LifeGrid.Presentation/AppShell.xaml.cs`

Add inside the constructor:
```csharp
Routing.RegisterRoute("week-detail", typeof(WeeklyHabitsPage));
```

**H2. Update:** `src/LifeGrid.Presentation/MauiProgram.cs`

Add:
```csharp
builder.Services.AddTransient<WeeklyHabitsViewModel>();
builder.Services.AddTransient<WeeklyHabitsPage>();
```

---

### Phase I — Build & Verify

```
dotnet build LifeGrid.slnx
dotnet test
```

Expected: 0 build errors; 257 prior tests + 3 new `GetWeeklyHabitsQueryTests` = **260 tests pass**.

**Manual verification checklist:**

1. Timeline: tap a week card → card highlights; "VIEW WEEK DETAIL" button appears at the bottom of the card.
2. Tap "VIEW WEEK DETAIL" → `WeeklyHabitsPage` pushes onto the stack showing: week date header + status/SP row.
3. Goal groups displayed in nested blocks: goal description + "Week N" label + metrics row.
4. Within each group, habit cards show: type label, name, description, target block.
5. Completion log rows display below the target block; proof image icon hidden when URL is null.
6. Tap proof image icon → full-screen overlay opens with image; tap "×" dismisses.
7. Navigate from a filtered Timeline (single-goal filter active) → `WeeklyHabitsPage` shows only that goal's group.
8. Android back button / shell back → returns to Timeline; scroll position preserved.

---

## Files Changed Summary

| File | Change |
|---|---|
| `src/LifeGrid.Domain/Habit/CompletedValueLog.cs` | **New** |
| `src/LifeGrid.Domain/Habit/Habit.cs` | Add `CompletedValuesLog`; update `Create()` signature |
| `src/LifeGrid.Infrastructure/Data/EntityConfigurations/CompletedValueLogConfiguration.cs` | **New** |
| `src/LifeGrid.Infrastructure/Data/EntityConfigurations/HabitConfiguration.cs` | Add `HasMany` navigation |
| `src/LifeGrid.Infrastructure/Data/LifeGridDbContext.cs` | Add `CompletedValueLogs` DbSet |
| `src/LifeGrid.Application/Habit/IHabitRepository.cs` | Add `GetByWeekGoalIdsAsync` |
| `src/LifeGrid.Infrastructure/Data/Repositories/HabitRepository.cs` | Implement `GetByWeekGoalIdsAsync` |
| `src/LifeGrid.Application/Week/IWeekRepository.cs` | Add `GetByIdAsync` |
| `src/LifeGrid.Infrastructure/Data/Repositories/WeekRepository.cs` | Implement `GetByIdAsync` |
| `src/LifeGrid.Application/Goal/RecalculateGoalScheduleCommand.cs` | Update `Habit.Create()` call |
| `src/LifeGrid.Application/Week/Commands/GenerateScheduleCommand.cs` | Update `Habit.Create()` call |
| `src/LifeGrid.Application/WeeklyHabits/WeeklyHabitsDashboardDto.cs` | **New** |
| `src/LifeGrid.Application/WeeklyHabits/GetWeeklyHabitsQuery.cs` | **New** |
| `src/LifeGrid.Application/WeeklyHabits/GetWeeklyHabitsQueryHandler.cs` | **New** |
| `src/LifeGrid.Presentation/ViewModels/HabitCompletionLogItem.cs` | **New** |
| `src/LifeGrid.Presentation/ViewModels/WeeklyHabitItem.cs` | **New** |
| `src/LifeGrid.Presentation/ViewModels/WeeklyGoalGroupItem.cs` | **New** |
| `src/LifeGrid.Presentation/ViewModels/WeeklyHabitsViewModel.cs` | **New** |
| `src/LifeGrid.Presentation/Pages/WeeklyHabitsPage.xaml` | **New** |
| `src/LifeGrid.Presentation/Pages/WeeklyHabitsPage.xaml.cs` | **New** |
| `src/LifeGrid.Presentation/ViewModels/TimelineViewModel.cs` | Add `DrillDownToWeekCommand` |
| `src/LifeGrid.Presentation/Pages/TimelinePage.xaml` | Add "VIEW WEEK DETAIL" button |
| `src/LifeGrid.Presentation/AppShell.xaml.cs` | Register `"week-detail"` route |
| `src/LifeGrid.Presentation/MauiProgram.cs` | Register `WeeklyHabitsViewModel` + `WeeklyHabitsPage` |
| `tests/LifeGrid.Domain.Tests/Habit/HabitEntityTests.cs` | Update `Habit.Create()` calls |
| `tests/LifeGrid.Infrastructure.Tests/Data/HabitRepositoryTests.cs` | Update `Habit.Create()` calls |
| `tests/LifeGrid.Infrastructure.Tests/Data/FactoryResetServiceTests.cs` | Update `Habit.Create()` calls |
| `tests/LifeGrid.Application.Tests/WeeklyHabits/GetWeeklyHabitsQueryTests.cs` | **New** — 3 tests |
