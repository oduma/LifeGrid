# Phase 11 — High-Fidelity Goal Card
## Implementation Plan

**Status:** DONE — completed 2026-06-21

**Finalized requirements:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` § Phase 11 (P11.1–P11.7)

---

## Pre-Flight Analysis (LEAD_ARCHITECT)

### Scope
Read-side extension of the `GoalManagement` bounded context plus Presentation UI replacement. No domain entity changes. No EF migrations. No new bounded context.

**Layers:**
- `LifeGrid.Application` — DTO extended, interface extended, query handler updated
- `LifeGrid.Infrastructure` — one new repository read method (no migration)
- `LifeGrid.Presentation` — ViewModel fields, new converter, XAML card layout
- `LifeGrid.Application.Tests` — new handler test class (4 facts)

### Breaking Changes
`GoalSummaryDto` gains a new required positional field `TotalWeeks`. Consumers:
- `GetGoalsQuery` handler (Application) — updated in Phase A3
- `GoalsViewModel.LoadAsync()` (Presentation) — updated in Phase C2

No existing test file for `GetGoalsQuery` was found; the new handler test class (Phase D) starts fresh.

### Key Decisions
- **TotalWeeks** = persisted DB count via `WeekGoals.Count(wg => wg.GoalId == goalId)` — not a date-range estimate.
- **Column order** per spec §2.2.C: DURATION | DEADLINE | TOTAL WKS.
- **StatusToColorConverter** hardcodes design token hex values; does NOT use `Application.Current.Resources` lookup (avoids MAUI runtime dep in hot paths).
- **Converter tests** deferred to future `LifeGrid.Presentation.Tests` project (established project pattern).

---

## Phase A — Application Layer

### A1 · `src/LifeGrid.Application/Goal/GoalSummaryDto.cs`

Add `TotalWeeks` as the last positional field:
```csharp
public record GoalSummaryDto(
    Guid     GoalId,
    string   Description,
    string   AmbientTag,
    string   Duration,
    DateTime DeadlineDate,
    string   Status,
    int      TotalWeeks);   // ← NEW
```

---

### A2 · `src/LifeGrid.Application/Week/IWeekRepository.cs`

Add one new read-only method signature after `GetByStartDateAsync`:
```csharp
Task<int> GetWeekGoalCountByGoalIdAsync(Guid goalId, CancellationToken ct = default);
```

---

### A3 · `src/LifeGrid.Application/Goal/GetGoalsQuery.cs`

Update `GetGoalsQueryHandler`:
- Add `IWeekRepository weekRepository` as the third constructor parameter.
- Replace the LINQ `.Select(...)` with a `foreach` loop that awaits `GetWeekGoalCountByGoalIdAsync` per goal.

```csharp
public sealed class GetGoalsQueryHandler(
    IUserProfileRepository userProfileRepository,
    IGoalRepository        goalRepository,
    IWeekRepository        weekRepository)
    : IRequestHandler<GetGoalsQuery, Result<IReadOnlyList<GoalSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<GoalSummaryDto>>> Handle(
        GetGoalsQuery     request,
        CancellationToken cancellationToken)
    {
        var profile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (profile is null)
            return Result<IReadOnlyList<GoalSummaryDto>>.Success(Array.Empty<GoalSummaryDto>());

        var goals = await goalRepository.GetAllByUserIdAsync(profile.UserId, cancellationToken);

        var dtos = new List<GoalSummaryDto>(goals.Count);
        foreach (var g in goals)
        {
            var totalWeeks = await weekRepository.GetWeekGoalCountByGoalIdAsync(g.GoalId, cancellationToken);
            dtos.Add(new GoalSummaryDto(
                g.GoalId,
                g.Description,
                g.AmbientTag,
                g.Duration,
                g.DeadlineDate,
                g.Status.ToString(),
                totalWeeks));
        }

        return Result<IReadOnlyList<GoalSummaryDto>>.Success(dtos);
    }
}
```

---

## Phase B — Infrastructure Layer

### B1 · `src/LifeGrid.Infrastructure/Data/Repositories/WeekRepository.cs`

Add the new interface method implementation:
```csharp
public Task<int> GetWeekGoalCountByGoalIdAsync(Guid goalId, CancellationToken ct = default)
    => db.WeekGoals.CountAsync(wg => wg.GoalId == goalId, ct);
```

---

## Phase C — Presentation Layer

### C1 · `src/LifeGrid.Presentation/ViewModels/GoalSummaryItem.cs`

Add `TotalWeeks` as the last positional field:
```csharp
public record GoalSummaryItem(
    Guid     GoalId,
    string   Description,
    string   AmbientTag,
    string   Duration,
    DateTime DeadlineDate,
    string   Status,
    int      TotalWeeks);  // ← NEW
```

---

### C2 · `src/LifeGrid.Presentation/ViewModels/GoalsViewModel.cs`

Update `LoadAsync()` — add `dto.TotalWeeks` as the 7th argument:
```csharp
Goals.Add(new GoalSummaryItem(
    dto.GoalId,
    dto.Description,
    dto.AmbientTag,
    dto.Duration,
    dto.DeadlineDate,
    dto.Status,
    dto.TotalWeeks));   // ← NEW
```

---

### C3 · `src/LifeGrid.Presentation/Converters/StatusToColorConverter.cs` (NEW FILE)

```csharp
using System.Globalization;

namespace LifeGrid.Presentation.Converters;

public sealed class StatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value as string) switch
        {
            "Active"      => Color.FromArgb("#35f8db"),  // Primary       — "active states"
            "Overwhelmed" => Color.FromArgb("#a20ba0"),  // On-Secondary  — warning/stress
            "Abandoned"   => Color.FromArgb("#FF1B77"),  // Error         — "destructive actions"
            "Completed"   => Color.FromArgb("#58585a"),  // On-Surface    — neutral/closed
            _             => Color.FromArgb("#58585a")   // fallback
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

---

### C4 · `src/LifeGrid.Presentation/Pages/GoalsPage.xaml`

Full replacement of the file. Key changes from the current version:
1. Add `xmlns:converters` namespace declaration.
2. Add `<ContentPage.Resources>` block with `StatusToColorConverter`.
3. Replace the simple `VerticalStackLayout` item template with the 3-zone `Border` card.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:controls="clr-namespace:LifeGrid.Presentation.Controls"
    xmlns:converters="clr-namespace:LifeGrid.Presentation.Converters"
    x:Class="LifeGrid.Presentation.Pages.GoalsPage"
    Title="Goals"
    BackgroundColor="{StaticResource Background}">

    <ContentPage.Resources>
        <converters:StatusToColorConverter x:Key="StatusToColorConverter" />
    </ContentPage.Resources>

    <Grid RowDefinitions="*,Auto">
        <Grid Grid.Row="0" RowDefinitions="*,Auto">

            <CollectionView Grid.Row="0" ItemsSource="{Binding Goals}">
                <CollectionView.EmptyView>
                    <Label
                        Text="No goals yet"
                        HorizontalOptions="Center"
                        VerticalOptions="Center"
                        HorizontalTextAlignment="Center" />
                </CollectionView.EmptyView>

                <CollectionView.ItemTemplate>
                    <DataTemplate>
                        <!-- Card container: Surface bg, 2px radius, subtle border -->
                        <Border Margin="16,8" Padding="16"
                                BackgroundColor="{StaticResource Surface}"
                                StrokeShape="RoundRectangle 2"
                                StrokeThickness="1"
                                Stroke="{StaticResource SurfaceBrush}">
                            <VerticalStackLayout Spacing="8">

                                <!-- Zone A: AmbientTag (left) | Status badge (right) -->
                                <Grid ColumnDefinitions="*,Auto">
                                    <Label
                                        Grid.Column="0"
                                        Text="{Binding AmbientTag}"
                                        FontFamily="{StaticResource FontDMMono}"
                                        FontSize="12"
                                        TextColor="{StaticResource OnSurface}"
                                        Opacity="0.6"
                                        VerticalOptions="Center" />
                                    <Border
                                        Grid.Column="1"
                                        Padding="6,2"
                                        StrokeShape="RoundRectangle 2"
                                        StrokeThickness="1"
                                        Stroke="{Binding Status, Converter={StaticResource StatusToColorConverter}}">
                                        <Label
                                            Text="{Binding Status}"
                                            FontFamily="{StaticResource FontShareTechMono}"
                                            FontSize="11"
                                            TextColor="{Binding Status, Converter={StaticResource StatusToColorConverter}}" />
                                    </Border>
                                </Grid>

                                <!-- Zone B: Description — dominant DM Mono headline -->
                                <Label
                                    Text="{Binding Description}"
                                    FontFamily="{StaticResource FontDMMono}"
                                    FontSize="22"
                                    FontAttributes="Bold"
                                    TextColor="{StaticResource OnSurface}"
                                    LineHeight="1.3" />

                                <!-- Zone C: Metric columns (DURATION | DEADLINE | TOTAL WKS) -->
                                <Grid ColumnDefinitions="*,*,*" Margin="0,8,0,0">

                                    <!-- Col 1: Duration -->
                                    <VerticalStackLayout Grid.Column="0" Spacing="2">
                                        <Label
                                            Text="DURATION"
                                            FontFamily="{StaticResource FontShareTechMono}"
                                            FontSize="10"
                                            TextColor="{StaticResource OnSurface}"
                                            Opacity="0.5" />
                                        <Label
                                            Text="{Binding Duration}"
                                            FontFamily="{StaticResource FontShareTechMono}"
                                            FontSize="14"
                                            FontAttributes="Bold"
                                            TextColor="{StaticResource OnSurface}" />
                                    </VerticalStackLayout>

                                    <!-- Col 2: Deadline -->
                                    <VerticalStackLayout Grid.Column="1" Spacing="2">
                                        <Label
                                            Text="DEADLINE"
                                            FontFamily="{StaticResource FontShareTechMono}"
                                            FontSize="10"
                                            TextColor="{StaticResource OnSurface}"
                                            Opacity="0.5" />
                                        <Label
                                            Text="{Binding DeadlineDate, StringFormat='{0:dd MMM yyyy}'}"
                                            FontFamily="{StaticResource FontShareTechMono}"
                                            FontSize="14"
                                            FontAttributes="Bold"
                                            TextColor="{StaticResource OnSurface}" />
                                    </VerticalStackLayout>

                                    <!-- Col 3: Total Weeks — Primary accent highlight -->
                                    <VerticalStackLayout Grid.Column="2" Spacing="2">
                                        <Label
                                            Text="TOTAL WKS"
                                            FontFamily="{StaticResource FontShareTechMono}"
                                            FontSize="10"
                                            TextColor="{StaticResource OnSurface}"
                                            Opacity="0.5" />
                                        <Label
                                            Text="{Binding TotalWeeks}"
                                            FontFamily="{StaticResource FontShareTechMono}"
                                            FontSize="14"
                                            FontAttributes="Bold"
                                            TextColor="{StaticResource Primary}" />
                                    </VerticalStackLayout>

                                </Grid>

                            </VerticalStackLayout>
                        </Border>
                    </DataTemplate>
                </CollectionView.ItemTemplate>
            </CollectionView>

            <Button
                Grid.Row="1"
                Text="Add Goal"
                Command="{Binding AddGoalCommand}"
                Margin="16,8" />

        </Grid>

        <controls:AdBannerView Grid.Row="1" />
    </Grid>

</ContentPage>
```

---

## Phase D — Tests

### D1 · NEW: `tests/LifeGrid.Application.Tests/Goal/GetGoalsQueryHandlerTests.cs`

Write BEFORE modifying `GetGoalsQuery.cs` (TDD).

```csharp
using FluentAssertions;
using LifeGrid.Application.Goal;
using LifeGrid.Application.UserProfile;
using LifeGrid.Application.Week;
using NSubstitute;
using GoalAggregate      = LifeGrid.Domain.Goal.Goal;
using UserProfileEntity  = LifeGrid.Domain.UserProfile.UserProfile;

namespace LifeGrid.Application.Tests.Goal;

public sealed class GetGoalsQueryHandlerTests
{
    private readonly IUserProfileRepository _profileRepo = Substitute.For<IUserProfileRepository>();
    private readonly IGoalRepository        _goalRepo    = Substitute.For<IGoalRepository>();
    private readonly IWeekRepository        _weekRepo    = Substitute.For<IWeekRepository>();
    private readonly GetGoalsQueryHandler   _handler;

    public GetGoalsQueryHandlerTests()
        => _handler = new GetGoalsQueryHandler(_profileRepo, _goalRepo, _weekRepo);

    [Fact]
    public async Task NoProfile_ReturnsEmptyList()
    {
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns((UserProfileEntity?)null);

        var result = await _handler.Handle(new GetGoalsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        await _goalRepo.DidNotReceive().GetAllByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoGoals_ReturnsEmptyList()
    {
        var profile = UserProfileEntity.Create();
        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goalRepo.GetAllByUserIdAsync(profile.UserId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<GoalAggregate>());

        var result = await _handler.Handle(new GetGoalsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task WithGoals_MapsTotalWeeks()
    {
        var profile = UserProfileEntity.Create();
        var goal    = GoalAggregate.Create(profile.UserId, "Run", "#Fitness", "6 months",
            new DateTime(2027, 1, 1), DateTime.Now);

        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goalRepo.GetAllByUserIdAsync(profile.UserId, Arg.Any<CancellationToken>())
            .Returns(new[] { goal });
        _weekRepo.GetWeekGoalCountByGoalIdAsync(goal.GoalId, Arg.Any<CancellationToken>())
            .Returns(12);

        var result = await _handler.Handle(new GetGoalsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value![0].TotalWeeks.Should().Be(12);
    }

    [Fact]
    public async Task WithGoals_MapsAllDtoFields()
    {
        var profile  = UserProfileEntity.Create();
        var deadline = new DateTime(2027, 6, 1);
        var goal     = GoalAggregate.Create(profile.UserId, "Learn Spanish", "#Language", "12 months",
            deadline, DateTime.Now);

        _profileRepo.GetSingleAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _goalRepo.GetAllByUserIdAsync(profile.UserId, Arg.Any<CancellationToken>())
            .Returns(new[] { goal });
        _weekRepo.GetWeekGoalCountByGoalIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(0);

        var result = await _handler.Handle(new GetGoalsQuery(), default);

        var dto = result.Value![0];
        dto.Description.Should().Be("Learn Spanish");
        dto.AmbientTag.Should().Be("#Language");
        dto.Duration.Should().Be("12 months");
        dto.DeadlineDate.Should().Be(deadline);
        dto.Status.Should().Be("Active");
    }
}
```

### D2 · NOTE: StatusToColorConverterTests — Deferred

`StatusToColorConverter` is in `LifeGrid.Presentation` (target: `net10.0-android`) and cannot be referenced from the `net10.0` test projects. Per the established pattern in `tests/LifeGrid.Application.Tests/UserSetup/UserSetupViewModelTests.cs`, visual/binding verification for Presentation types is deferred to a future `LifeGrid.Presentation.Tests` project.

---

## Execution Order

```
D1 (write failing tests) →
A1 (GoalSummaryDto.TotalWeeks) →
A2 (IWeekRepository.GetWeekGoalCountByGoalIdAsync) →
A3 (GetGoalsQueryHandler — inject IWeekRepository, await count per goal) →
B1 (WeekRepository.GetWeekGoalCountByGoalIdAsync implementation) →
C1 (GoalSummaryItem.TotalWeeks) →
C2 (GoalsViewModel.LoadAsync mapping) →
C3 (StatusToColorConverter — new file) →
C4 (GoalsPage.xaml — full card layout) →
dotnet build →
dotnet test
```

---

## Estimated Test Delta

| Test project | New tests | Updated tests |
|---|---|---|
| Application.Tests | 4 (`GetGoalsQueryHandlerTests`) | 0 |
| **Total new** | **4** | |

Estimated totals: 213 existing + 4 new = **217 tests**.

---

## Post-Implementation Results

**Build:** 0 errors. 3 pre-existing `CS0612` warnings on `ViceSurveyPage.xaml` — not Phase 11.

**Tests:** 217 passing (83 Domain + 70 Application + 64 Infrastructure). +4 new tests as estimated.

| Test project | New tests (actual) |
|---|---|
| Application.Tests | 4 (`GetGoalsQueryHandlerTests`: NoProfile, NoGoals, MapsTotalWeeks, MapsAllDtoFields) |
| **Total new** | **4** |

**Implementation Notes (resolved)**

| Issue | Resolution |
|---|---|
| Zone C column alignment not specified | User correction post-implementation: DURATION anchored `Start`, DEADLINE anchored `Center`, TOTAL WKS anchored `End` — uses full card width. |
