# Phase 21 — Home View: Current Week Active Dashboard

**Status:** DONE (implemented 2026-06-23)
**Date:** 2026-06-23

---

## Post-Implementation Corrections

| Area | Plan | Actual / Fix |
|---|---|---|
| `WeekRepository.GetByStartDateAsync` | No eager loading specified | Missing `.Include(w => w.WeekGoals)` — week was found but `WeekGoals` collection was always empty; handler returned zero goal groups; Home showed empty state even with active goals |
| Timeline current-week detection | `GoalAggregate.CalculateStartDate(DateTime.Today)` | That method is forward-looking (returns next Monday for non-Monday days); replaced with preceding-Monday formula `today.AddDays(-((int)today.DayOfWeek - Monday + 7) % 7)` in `TimelineViewModel` only — `CalculateStartDate` itself is unchanged and still used correctly in goal creation |
| Post-onboarding and startup navigation | Not specified in Phase 21 requirements | `App.xaml.cs` (returning user startup) and `SetupViewModel.cs` (first-time onboarding completion) both navigated to `"//goals"`; changed to `"//home"`. `UserSetupViewModel.EditActiveGoalsAsync` intentionally navigates to `"//goals"` and was left unchanged |

---

## 1. Overview

Phase 21 wires up the app's root landing page (`HomePage`) to automatically display the user's active weekly habit dashboard. It introduces a `GetCurrentWeekHabitsQuery` that resolves the current Monday-anchored week by date (no navigation parameters), displays the same Zone A/B/C layout built in Phase 20, and handles the empty state when no goals exist for the current week.

No new database tables or EF migrations are required.

---

## 2. Pre-Flight Architecture Analysis (LEAD_ARCHITECT)

**Bounded Context:** `HomeManagement` (new) → pulls from `WeeklyHabits` DTOs defined in Phase 20. No domain model changes.

**Persona assignments:**
- Phase 1 (Application layer): `SENIOR_NET_DEVELOPER`
- Phase 2 (Infrastructure): `SENIOR_NET_DEVELOPER`
- Phase 3 (Presentation): `MAUI_UX_ENGINEER`
- Phase 4 (Tests): `TDD_SPECIALIST`

**Key decision: clock abstraction.**
`IDateTimeProvider` interface in `LifeGrid.Application.Common` (injected into handler). `SystemDateTimeProvider` in Infrastructure returns `DateTime.UtcNow`. Registered as Singleton.

**Key decision: preceding Monday math.**
`GetCurrentWeekHabitsQueryHandler` uses `today.AddDays(-((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7)`. This is the inverse of `GoalAggregate.CalculateStartDate` (which is forward-looking for goal creation). The preceding-Monday formula is inlined in the handler, not added to the Domain.

**Key decision: ViewModel separation.**
`HomeViewModel` is a distinct class, not an extension of `WeeklyHabitsViewModel`. It has no `IQueryAttributable` (root tab, not a pushed page). Tab lifecycle: `OnAppearing` fires before `ApplyQueryAttributes`, but since there are no query params, `OnAppearing` calls `LoadAsync()` directly.

**Key decision: empty state.**
Handler returns `Result.Failure` when no week is found. ViewModel treats `!result.IsSuccess || GoalGroups.Count == 0` as the empty state trigger, setting `IsEmptyStateVisible=true`, `IsWeeklyDataVisible=false`.

**Key decision: aggregation logic.**
`GetCurrentWeekHabitsQueryHandler` inlines the same goal/habit aggregation as `GetWeeklyHabitsQueryHandler` (no filter applied). MediatR handler-calls-handler is an anti-pattern; small code duplication is acceptable per KISS.

---

## 3. Clarifications

| Question | Answer |
|---|---|
| Clock abstraction style | `IDateTimeProvider` interface (DI-registered Singleton in Infrastructure) |
| ViewModel architecture | Distinct `HomeViewModel` (no reuse of `WeeklyHabitsViewModel`) |

---

## 4. Files To Create / Modify

| # | Action | Path |
|---|---|---|
| 1 | CREATE | `src/LifeGrid.Application/Common/IDateTimeProvider.cs` |
| 2 | CREATE | `src/LifeGrid.Application/Home/GetCurrentWeekHabitsQuery.cs` |
| 3 | CREATE | `src/LifeGrid.Application/Home/GetCurrentWeekHabitsQueryHandler.cs` |
| 4 | CREATE | `src/LifeGrid.Infrastructure/Data/Services/SystemDateTimeProvider.cs` |
| 5 | UPDATE | `src/LifeGrid.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` |
| 6 | CREATE | `src/LifeGrid.Presentation/ViewModels/HomeViewModel.cs` |
| 7 | UPDATE | `src/LifeGrid.Presentation/Pages/HomePage.xaml` |
| 8 | UPDATE | `src/LifeGrid.Presentation/Pages/HomePage.xaml.cs` |
| 9 | UPDATE | `src/LifeGrid.Presentation/MauiProgram.cs` |
| 10 | CREATE | `tests/LifeGrid.Application.Tests/Home/GetCurrentWeekHabitsQueryTests.cs` |

---

## 5. Implementation Phases

### Phase 1 — Application Layer

**Step 1.1 — `IDateTimeProvider`**

Create `src/LifeGrid.Application/Common/IDateTimeProvider.cs`:
```csharp
namespace LifeGrid.Application.Common;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
```

**Step 1.2 — `GetCurrentWeekHabitsQuery`**

Create `src/LifeGrid.Application/Home/GetCurrentWeekHabitsQuery.cs`:
```csharp
using LifeGrid.Application.WeeklyHabits;
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Home;

public record GetCurrentWeekHabitsQuery : IRequest<Result<WeeklyHabitsDashboardDto>>;
```

**Step 1.3 — `GetCurrentWeekHabitsQueryHandler`**

Create `src/LifeGrid.Application/Home/GetCurrentWeekHabitsQueryHandler.cs`:

```csharp
using LifeGrid.Application.Common;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Habit;
using LifeGrid.Application.Week;
using LifeGrid.Application.WeeklyHabits;
using LifeGrid.Domain.Common;
using MediatR;
using HabitEntity = LifeGrid.Domain.Habit.Habit;

namespace LifeGrid.Application.Home;

public sealed class GetCurrentWeekHabitsQueryHandler(
    IWeekRepository    weekRepository,
    IGoalRepository    goalRepository,
    IHabitRepository   habitRepository,
    IDateTimeProvider  dateTimeProvider)
    : IRequestHandler<GetCurrentWeekHabitsQuery, Result<WeeklyHabitsDashboardDto>>
{
    public async Task<Result<WeeklyHabitsDashboardDto>> Handle(
        GetCurrentWeekHabitsQuery request,
        CancellationToken         cancellationToken)
    {
        var today         = dateTimeProvider.UtcNow.Date;
        int daysFromMon   = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var currentMonday = today.AddDays(-daysFromMon);

        var week = await weekRepository.GetByStartDateAsync(currentMonday, cancellationToken);
        if (week is null)
            return Result<WeeklyHabitsDashboardDto>.Failure("No active week found.");

        var weekGoals  = week.WeekGoals.ToList();
        var goalIds    = weekGoals.Select(wg => wg.GoalId).Distinct().ToList();
        var goals      = await goalRepository.GetByIdsAsync(goalIds, cancellationToken);
        var descMap    = goals.ToDictionary(g => g.GoalId, g => g.Description);

        var weekGoalIds  = weekGoals.Select(wg => wg.WeekGoalId).ToList();
        var habits       = await habitRepository.GetByWeekGoalIdsAsync(weekGoalIds, cancellationToken);
        var habitsByWgId = habits
            .GroupBy(h => h.WeekGoalId)
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
                    h.HabitId, h.HabitType.ToString(), h.HabitName, h.HabitDescription,
                    h.TargetValue, h.MeasurementUnit, h.DeadlineDateTime,
                    h.CompletedValuesLog.Select(l => new HabitCompletionLogDto(
                        l.LogId, l.ActualValue, l.MeasurementUnit,
                        l.ProofText, l.ProofImageUrl, l.Timestamp)).ToList()
                )).ToList());
        }).ToList();

        return Result<WeeklyHabitsDashboardDto>.Success(new WeeklyHabitsDashboardDto(
            week.WeekId, week.StartDate, week.Status.ToString(),
            week.TotalWeeklySpEarned, groups));
    }
}
```

---

### Phase 2 — Infrastructure Layer

**Step 2.1 — `SystemDateTimeProvider`**

Create `src/LifeGrid.Infrastructure/Data/Services/SystemDateTimeProvider.cs`:
```csharp
using LifeGrid.Application.Common;

namespace LifeGrid.Infrastructure.Data.Services;

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
```

**Step 2.2 — Register in DI**

In `InfrastructureServiceExtensions.cs`, add **before** the existing repository registrations:
```csharp
services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
```

---

### Phase 3 — Presentation Layer

**Step 3.1 — `HomeViewModel`**

Create `src/LifeGrid.Presentation/ViewModels/HomeViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeGrid.Application.Home;
using MediatR;
using System.Collections.ObjectModel;

namespace LifeGrid.Presentation.ViewModels;

public partial class HomeViewModel(IMediator mediator) : ObservableObject
{
    [ObservableProperty] private string  _weekHeaderText            = string.Empty;
    [ObservableProperty] private string  _weekStatusText            = string.Empty;
    [ObservableProperty] private bool    _isEmptyStateVisible;
    [ObservableProperty] private bool    _isWeeklyDataVisible;
    [ObservableProperty] private string? _proofImageUrl;
    [ObservableProperty] private bool    _isProofImageOverlayVisible;

    public ObservableCollection<WeeklyGoalGroupItem> GoalGroups { get; } = new();

    public async Task LoadAsync()
    {
        var result  = await mediator.Send(new GetCurrentWeekHabitsQuery());
        var hasData = result.IsSuccess && result.Value?.GoalGroups.Count > 0;

        IsWeeklyDataVisible = hasData;
        IsEmptyStateVisible = !hasData;

        if (!hasData)
        {
            GoalGroups.Clear();
            return;
        }

        var dto = result.Value!;
        WeekHeaderText = $"Current Week — {dto.StartDate:MMM dd, yyyy}";
        WeekStatusText = $"{dto.Status}  |  SP: {dto.TotalWeeklySpEarned}";

        GoalGroups.Clear();
        foreach (var g in dto.GoalGroups)
            GoalGroups.Add(new WeeklyGoalGroupItem(g));
    }

    [RelayCommand]
    private async Task NavigateToCreateGoalAsync()
        => await Shell.Current.GoToAsync("create-goal");

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

**Step 3.2 — `HomePage.xaml`**

Replace the placeholder content entirely. The structure mirrors `WeeklyHabitsPage.xaml` with two additions: `IsVisible` bindings on the scrollable zone, and an empty state panel.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:vm="clr-namespace:LifeGrid.Presentation.ViewModels"
    xmlns:controls="clr-namespace:LifeGrid.Presentation.Controls"
    x:Class="LifeGrid.Presentation.Pages.HomePage"
    x:Name="PageRoot"
    Title="Home"
    BackgroundColor="{StaticResource Background}">

    <Grid RowDefinitions="Auto,*,Auto">

        <!-- Zone A: Week header (fixed) -->
        <Border
            Grid.Row="0"
            Margin="16,8,16,4"
            Padding="12,10"
            BackgroundColor="{StaticResource Surface}"
            StrokeShape="RoundRectangle 2"
            StrokeThickness="1"
            Stroke="{StaticResource SurfaceBrush}">
            <Grid ColumnDefinitions="*,Auto">
                <Label
                    Grid.Column="0"
                    Text="{Binding WeekHeaderText}"
                    FontFamily="{StaticResource FontDMMono}"
                    FontSize="15"
                    FontAttributes="Bold"
                    TextColor="{StaticResource OnSurface}"
                    VerticalOptions="Center" />
                <Label
                    Grid.Column="1"
                    Text="{Binding WeekStatusText}"
                    FontFamily="{StaticResource FontShareTechMono}"
                    FontSize="12"
                    TextColor="{StaticResource Primary}"
                    VerticalOptions="Center"
                    HorizontalTextAlignment="End" />
            </Grid>
        </Border>

        <!-- Zone B+C: Scrollable goal groups — visible when data exists -->
        <ScrollView
            Grid.Row="1"
            IsVisible="{Binding IsWeeklyDataVisible}">
            <VerticalStackLayout
                BindableLayout.ItemsSource="{Binding GoalGroups}"
                Spacing="20"
                Padding="16,8,16,16">

                <BindableLayout.ItemTemplate>
                    <DataTemplate x:DataType="vm:WeeklyGoalGroupItem">
                        <VerticalStackLayout Spacing="8">

                            <Grid ColumnDefinitions="*,Auto" Margin="0,4,0,0">
                                <Label
                                    Grid.Column="0"
                                    Text="{Binding GoalDescription}"
                                    FontFamily="{StaticResource FontDMMono}"
                                    FontSize="15"
                                    FontAttributes="Bold"
                                    TextColor="{StaticResource OnSurface}"
                                    VerticalOptions="Center" />
                                <Label
                                    Grid.Column="1"
                                    Text="{Binding WeekLabel}"
                                    FontFamily="{StaticResource FontShareTechMono}"
                                    FontSize="11"
                                    TextColor="{StaticResource OnSurface}"
                                    Opacity="0.55"
                                    VerticalOptions="Center" />
                            </Grid>

                            <Label
                                Text="{Binding MetricsText}"
                                FontFamily="{StaticResource FontShareTechMono}"
                                FontSize="12"
                                TextColor="{StaticResource OnSurface}"
                                Opacity="0.75">
                                <Label.Triggers>
                                    <DataTrigger TargetType="Label" Binding="{Binding IsInPenalty}" Value="True">
                                        <Setter Property="TextColor" Value="#FF7A00" />
                                        <Setter Property="Opacity" Value="1.0" />
                                    </DataTrigger>
                                </Label.Triggers>
                            </Label>

                            <VerticalStackLayout
                                BindableLayout.ItemsSource="{Binding Habits}"
                                Spacing="8">

                                <BindableLayout.EmptyView>
                                    <Label
                                        Text="No habits scheduled"
                                        FontFamily="{StaticResource FontShareTechMono}"
                                        FontSize="11"
                                        TextColor="{StaticResource OnSurface}"
                                        Opacity="0.4" />
                                </BindableLayout.EmptyView>

                                <BindableLayout.ItemTemplate>
                                    <DataTemplate x:DataType="vm:WeeklyHabitItem">
                                        <Border
                                            Padding="12,10"
                                            BackgroundColor="{StaticResource Surface}"
                                            StrokeShape="RoundRectangle 2"
                                            StrokeThickness="1"
                                            Stroke="{StaticResource SurfaceBrush}">

                                            <VerticalStackLayout Spacing="6">
                                                <Grid ColumnDefinitions="Auto,*" ColumnSpacing="6">
                                                    <Border
                                                        Grid.Column="0"
                                                        Padding="4,2"
                                                        StrokeShape="RoundRectangle 2"
                                                        StrokeThickness="1"
                                                        Stroke="{StaticResource SurfaceBrush}"
                                                        VerticalOptions="Center">
                                                        <Label
                                                            Text="{Binding HabitTypeLabel}"
                                                            FontFamily="{StaticResource FontShareTechMono}"
                                                            FontSize="9"
                                                            TextColor="{StaticResource OnSurface}"
                                                            Opacity="0.6" />
                                                    </Border>
                                                    <Label
                                                        Grid.Column="1"
                                                        Text="{Binding HabitName}"
                                                        FontFamily="{StaticResource FontDMMono}"
                                                        FontSize="14"
                                                        FontAttributes="Bold"
                                                        TextColor="{StaticResource OnSurface}"
                                                        VerticalOptions="Center" />
                                                </Grid>

                                                <Label
                                                    Text="{Binding HabitDescription}"
                                                    FontFamily="{StaticResource FontShareTechMono}"
                                                    FontSize="12"
                                                    TextColor="{StaticResource OnSurface}"
                                                    Opacity="0.8"
                                                    LineBreakMode="WordWrap" />

                                                <Label
                                                    Text="{Binding TargetText}"
                                                    FontFamily="{StaticResource FontShareTechMono}"
                                                    FontSize="11"
                                                    TextColor="{StaticResource OnSurface}"
                                                    Opacity="0.55" />

                                                <VerticalStackLayout
                                                    BindableLayout.ItemsSource="{Binding CompletionLogs}"
                                                    Spacing="4"
                                                    Margin="0,4,0,0">
                                                    <BindableLayout.ItemTemplate>
                                                        <DataTemplate x:DataType="vm:HabitCompletionLogItem">
                                                            <Grid ColumnDefinitions="*,Auto" ColumnSpacing="8">
                                                                <VerticalStackLayout Grid.Column="0" Spacing="1">
                                                                    <Label
                                                                        Text="{Binding LogSummary}"
                                                                        FontFamily="{StaticResource FontShareTechMono}"
                                                                        FontSize="11"
                                                                        TextColor="{StaticResource Primary}" />
                                                                    <Label
                                                                        Text="{Binding ProofText}"
                                                                        FontFamily="{StaticResource FontShareTechMono}"
                                                                        FontSize="10"
                                                                        TextColor="{StaticResource OnSurface}"
                                                                        Opacity="0.6"
                                                                        IsVisible="{Binding HasProofText}"
                                                                        LineBreakMode="WordWrap" />
                                                                </VerticalStackLayout>
                                                                <Label
                                                                    Grid.Column="1"
                                                                    Text="&#xE3F4;"
                                                                    FontFamily="MaterialSymbolsRounded"
                                                                    FontSize="20"
                                                                    TextColor="{StaticResource Secondary}"
                                                                    IsVisible="{Binding HasProofImage}"
                                                                    VerticalOptions="Center">
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

        <!-- Empty state — visible when no active goals for current week -->
        <VerticalStackLayout
            Grid.Row="1"
            IsVisible="{Binding IsEmptyStateVisible}"
            VerticalOptions="Center"
            HorizontalOptions="Center"
            Spacing="20"
            Padding="32,0">
            <Label
                Text="No active goals or habits scheduled for this week."
                FontFamily="{StaticResource FontShareTechMono}"
                FontSize="14"
                TextColor="{StaticResource OnSurface}"
                Opacity="0.65"
                HorizontalTextAlignment="Center"
                LineBreakMode="WordWrap" />
            <Button
                Text="Create a Goal"
                Command="{Binding NavigateToCreateGoalCommand}"
                FontFamily="{StaticResource FontDMMono}"
                FontSize="13"
                FontAttributes="Bold"
                BackgroundColor="{StaticResource Primary}"
                TextColor="{StaticResource Background}"
                CornerRadius="2"
                HorizontalOptions="Center"
                Padding="24,10" />
        </VerticalStackLayout>

        <!-- Ad banner -->
        <controls:AdBannerView Grid.Row="2" />

        <!-- Proof image overlay — full-page, above all rows -->
        <Grid
            Grid.RowSpan="3"
            IsVisible="{Binding IsProofImageOverlayVisible}"
            BackgroundColor="#CC000000">
            <Image
                Source="{Binding ProofImageUrl}"
                Aspect="AspectFit"
                Margin="24"
                VerticalOptions="Center" />
            <Button
                Text="&#xE5CD;"
                FontFamily="MaterialSymbolsRounded"
                FontSize="22"
                Command="{Binding DismissProofImageCommand}"
                BackgroundColor="Transparent"
                TextColor="White"
                HorizontalOptions="End"
                VerticalOptions="Start"
                Margin="8" />
        </Grid>

    </Grid>

</ContentPage>
```

**Step 3.3 — `HomePage.xaml.cs`**

```csharp
using LifeGrid.Presentation.ViewModels;

namespace LifeGrid.Presentation.Pages;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel     = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.LoadAsync();
    }
}
```

**Step 3.4 — `MauiProgram.cs`**

Add both registrations alongside the existing tab page pairs (matching `TimelineViewModel`/`TimelinePage` pattern):
```csharp
builder.Services.AddTransient<HomeViewModel>();
builder.Services.AddTransient<HomePage>();
```

MAUI Shell uses the DI container to create `ContentTemplate` pages — `HomePage` now has a ViewModel constructor parameter so both registrations are required.

---

### Phase 4 — Tests

**Step 4.1 — `GetCurrentWeekHabitsQueryTests`**

Create `tests/LifeGrid.Application.Tests/Home/GetCurrentWeekHabitsQueryTests.cs`:

```csharp
using FluentAssertions;
using LifeGrid.Application.Common;
using LifeGrid.Application.Goal;
using LifeGrid.Application.Habit;
using LifeGrid.Application.Home;
using LifeGrid.Application.Week;
using NSubstitute;
using UserProfileEntity = LifeGrid.Domain.UserProfile.UserProfile;
using GoalAggregate     = LifeGrid.Domain.Goal.Goal;
using WeekEntity        = LifeGrid.Domain.Week.Week;
using WeekGoalEntity    = LifeGrid.Domain.WeekGoal.WeekGoal;
using HabitEntity       = LifeGrid.Domain.Habit.Habit;

namespace LifeGrid.Application.Tests.Home;

public sealed class GetCurrentWeekHabitsQueryTests
{
    private readonly IWeekRepository               _weekRepo        = Substitute.For<IWeekRepository>();
    private readonly IGoalRepository               _goalRepo        = Substitute.For<IGoalRepository>();
    private readonly IHabitRepository              _habitRepo       = Substitute.For<IHabitRepository>();
    private readonly IDateTimeProvider             _clock           = Substitute.For<IDateTimeProvider>();
    private readonly GetCurrentWeekHabitsQueryHandler _handler;

    public GetCurrentWeekHabitsQueryTests()
        => _handler = new GetCurrentWeekHabitsQueryHandler(_weekRepo, _goalRepo, _habitRepo, _clock);

    // ── temporal resolution ───────────────────────────────────────────────────

    [Fact]
    public async Task WednesdayDate_QueriesForPrecedingMonday()
    {
        // 2026-06-25 is a Thursday; 2026-06-22 is the preceding Monday
        // Use 2026-06-24 (Wednesday) → 2026-06-22 is the preceding Monday
        var wednesday = new DateTime(2026, 6, 24, 12, 0, 0, DateTimeKind.Utc); // Wednesday
        _clock.UtcNow.Returns(wednesday);
        _weekRepo.GetByStartDateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns((WeekEntity?)null);

        await _handler.Handle(new GetCurrentWeekHabitsQuery(), default);

        var expectedMonday = new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc);
        await _weekRepo.Received(1)
            .GetByStartDateAsync(Arg.Is<DateTime>(d => d.Date == expectedMonday.Date), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MondayDate_QueriesForSameDay()
    {
        var monday = new DateTime(2026, 6, 22, 8, 0, 0, DateTimeKind.Utc); // Monday
        _clock.UtcNow.Returns(monday);
        _weekRepo.GetByStartDateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns((WeekEntity?)null);

        await _handler.Handle(new GetCurrentWeekHabitsQuery(), default);

        await _weekRepo.Received(1)
            .GetByStartDateAsync(Arg.Is<DateTime>(d => d.Date == monday.Date), Arg.Any<CancellationToken>());
    }

    // ── null week ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoWeekFound_ReturnsFailureResult()
    {
        _clock.UtcNow.Returns(new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc));
        _weekRepo.GetByStartDateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns((WeekEntity?)null);

        var result = await _handler.Handle(new GetCurrentWeekHabitsQuery(), default);

        result.IsSuccess.Should().BeFalse();
    }

    // ── empty week ────────────────────────────────────────────────────────────

    [Fact]
    public async Task WeekWithNoGoals_ReturnsEmptyGoalGroups()
    {
        _clock.UtcNow.Returns(new DateTime(2026, 6, 23, 0, 0, 0, DateTimeKind.Utc)); // Monday
        var week = WeekEntity.Create(1, new DateTime(2026, 6, 23, 0, 0, 0, DateTimeKind.Utc));
        _weekRepo.GetByStartDateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                 .Returns(week);
        _goalRepo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(Array.Empty<GoalAggregate>());
        _habitRepo.GetByWeekGoalIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                  .Returns(Array.Empty<HabitEntity>());

        var result = await _handler.Handle(new GetCurrentWeekHabitsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.GoalGroups.Should().BeEmpty();
    }
}
```

---

## 6. Acceptance Criteria

- `dotnet build LifeGrid.slnx` → 0 errors.
- `dotnet test` → 260 prior + 4 new = **264 tests pass**.
- Launching the app after onboarding → Home tab is the active default tab.
- Home tab shows current-week header (`"Current Week — {date}"`) + all goal groups + habit cards.
- When no week or goals exist for the current Monday → empty state message + `"Create a Goal"` button rendered; tapping button navigates to Create Goal page.
- Proof image overlay functions identically to Phase 20 `WeeklyHabitsPage`.
- Switching to Home tab and back does not push a new page onto the navigation stack (MAUI Shell `ContentTemplate` tab guarantee; verified structurally — tab pages are never pushed).
