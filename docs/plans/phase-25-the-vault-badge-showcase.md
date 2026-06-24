# Phase 25 — The Vault: Badge & Achievement Showcase
Status: DONE

---

## Pre-Flight Analysis (LEAD_ARCHITECT)

### Bounded Context: UserProfile / Presentation
The Vault is a read-only display surface for `UserProfile.Badges`. Phase 25:
- Extends the `UserBadge` domain entity with an `IconName` property
- Adds `UserProfile.AwardBadge(...)` for future awarding (needed for testability now)
- Adds a `GetUserBadgesQuery` handler in Application
- Replaces the VaultPage placeholder with a fully rendered badge grid

### Why `IconName` goes on the domain entity
The requirement specifies `Icon_Name` as part of the badge data model. It belongs in the domain (not a presentation mapping) because it's a stable attribute of the awarded badge — the icon at award time should be preserved, not re-derived at render time.

### Dependencies
- Phase 4 established `UserBadge`, `UserProfile._badges`, and the `UserBadges` EF table
- Phase 25 adds one column (`IconName`) to that table via migration

### Risks
- `VaultViewModel` is in `LifeGrid.Presentation` (net10.0-android); ViewModel tests deferred (same pattern as Timeline and UserSetup)
- `GridItemsLayout.Span` is bindable in MAUI — confirmed; no workaround needed
- Dynamic span uses `DeviceDisplay.Current.MainDisplayInfo`; density guard added for simulator edge case

---

## Implementation Plan

### Phase 1 — Domain Layer (SENIOR_NET_DEVELOPER)

**Step 1.1 — UserBadge: add IconName + Create factory**

File: `src/LifeGrid.Domain/UserProfile/UserBadge.cs`

```csharp
public string IconName { get; private set; } = string.Empty;

public static UserBadge Create(string badgeType, string description, string iconName, DateTime dateEarned)
    => new()
    {
        BadgeId     = Guid.NewGuid(),
        BadgeType   = badgeType,
        Description = description,
        IconName    = iconName,
        DateEarned  = dateEarned
    };
```

**Step 1.2 — UserProfile: AwardBadge method**

File: `src/LifeGrid.Domain/UserProfile/UserProfile.cs`

```csharp
public void AwardBadge(string badgeType, string description, string iconName, DateTime dateEarned)
    => _badges.Add(UserBadge.Create(badgeType, description, iconName, dateEarned));
```

---

### Phase 2 — Application Layer (SENIOR_NET_DEVELOPER)

**Step 2.1 — BadgeDto**

File: `src/LifeGrid.Application/Badge/BadgeDto.cs`

```csharp
namespace LifeGrid.Application.Badge;

public record BadgeDto(Guid BadgeId, string BadgeType, string IconName, string Description, DateTime DateEarned);
```

**Step 2.2 — GetUserBadgesQuery + Handler**

File: `src/LifeGrid.Application/Badge/GetUserBadgesQuery.cs`

```csharp
using LifeGrid.Application.UserProfile;
using LifeGrid.Domain.Common;
using MediatR;

namespace LifeGrid.Application.Badge;

public record GetUserBadgesQuery : IRequest<Result<IReadOnlyCollection<BadgeDto>>>;

public sealed class GetUserBadgesQueryHandler(IUserProfileRepository userProfileRepository)
    : IRequestHandler<GetUserBadgesQuery, Result<IReadOnlyCollection<BadgeDto>>>
{
    public async Task<Result<IReadOnlyCollection<BadgeDto>>> Handle(
        GetUserBadgesQuery request, CancellationToken cancellationToken)
    {
        var profile = await userProfileRepository.GetSingleAsync(cancellationToken);
        if (profile is null || !profile.Badges.Any())
            return Result<IReadOnlyCollection<BadgeDto>>.Success(Array.Empty<BadgeDto>());

        var dtos = profile.Badges
            .Select(b => new BadgeDto(b.BadgeId, b.BadgeType, b.IconName, b.Description, b.DateEarned))
            .ToArray();

        return Result<IReadOnlyCollection<BadgeDto>>.Success(dtos);
    }
}
```

---

### Phase 3 — Infrastructure Layer (SENIOR_NET_DEVELOPER)

**Step 3.1 — UserProfileConfiguration: map IconName**

File: `src/LifeGrid.Infrastructure/Data/EntityConfigurations/UserProfileConfiguration.cs`

Inside `OwnsMany(e => e.Badges, badge => { ... })`, after the `DateEarned` property mapping, add:

```csharp
badge.Property(b => b.IconName).HasMaxLength(200);
```

**Step 3.2 — EF Migration**

```
dotnet ef migrations add Phase25_AddIconNameToBadge --project src/LifeGrid.Infrastructure
```

Expected migration content:
- Up: `AddColumn<string>("IconName", "UserBadges", nullable: false, defaultValue: "", maxLength: 200)`
- Down: `DropColumn("IconName", "UserBadges")`

---

### Phase 4 — Presentation Layer (MAUI_UX_ENGINEER)

**Step 4.1 — VaultBadgeItem**

File: `src/LifeGrid.Presentation/ViewModels/VaultBadgeItem.cs`

```csharp
namespace LifeGrid.Presentation.ViewModels;

public sealed class VaultBadgeItem
{
    public string   IconGlyph   { get; init; } = string.Empty;
    public string   Title       { get; init; } = string.Empty;
    public string   Description { get; init; } = string.Empty;
    public DateTime DateEarned  { get; init; }
}
```

**Step 4.2 — VaultViewModel**

File: `src/LifeGrid.Presentation/ViewModels/VaultViewModel.cs`

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using LifeGrid.Application.Badge;
using MediatR;
using System.Collections.ObjectModel;

namespace LifeGrid.Presentation.ViewModels;

public partial class VaultViewModel : ObservableObject
{
    private readonly IMediator _mediator;

    public VaultViewModel(IMediator mediator) { _mediator = mediator; }

    [ObservableProperty] private bool _isEmptyStateVisible;
    [ObservableProperty] private int  _gridSpan = 3;

    public ObservableCollection<VaultBadgeItem> Badges { get; } = new();

    public async Task LoadAsync()
    {
        var density = DeviceDisplay.Current.MainDisplayInfo.Density;
        var widthDp = density > 0
            ? DeviceDisplay.Current.MainDisplayInfo.Width / density
            : 360;
        GridSpan = widthDp >= 400 ? 4 : 3;

        var result = await _mediator.Send(new GetUserBadgesQuery());
        Badges.Clear();

        if (!result.IsSuccess || result.Value is null || !result.Value.Any())
        {
            IsEmptyStateVisible = true;
            return;
        }

        IsEmptyStateVisible = false;
        foreach (var dto in result.Value)
            Badges.Add(new VaultBadgeItem
            {
                IconGlyph   = dto.IconName,
                Title       = dto.BadgeType,
                Description = dto.Description,
                DateEarned  = dto.DateEarned
            });
    }
}
```

**Step 4.3 — VaultPage.xaml.cs**

File: `src/LifeGrid.Presentation/Pages/VaultPage.xaml.cs`

```csharp
using LifeGrid.Presentation.ViewModels;

namespace LifeGrid.Presentation.Pages;

public partial class VaultPage : ContentPage
{
    private readonly VaultViewModel _viewModel;

    public VaultPage(VaultViewModel viewModel)
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

**Step 4.4 — VaultPage.xaml**

Replace placeholder content (the inner `ScrollView` Row 0) with:

```xml
<ScrollView Grid.Row="0">
    <Grid>
        <Label
            Text="The Vault is empty. Stick to your grid to earn your first badge."
            IsVisible="{Binding IsEmptyStateVisible}"
            HorizontalOptions="Center"
            VerticalOptions="Center"
            HorizontalTextAlignment="Center"
            FontFamily="ShareTechMono"
            FontSize="14"
            TextColor="{StaticResource OnSurface}"
            Margin="24,48" />

        <CollectionView
            ItemsSource="{Binding Badges}"
            IsVisible="{Binding IsEmptyStateVisible, Converter={StaticResource InverseBoolConverter}}"
            Margin="16,16,16,0">
            <CollectionView.ItemsLayout>
                <GridItemsLayout
                    Orientation="Vertical"
                    Span="{Binding GridSpan}"
                    VerticalItemSpacing="16"
                    HorizontalItemSpacing="8" />
            </CollectionView.ItemsLayout>
            <CollectionView.ItemTemplate>
                <DataTemplate x:DataType="viewmodels:VaultBadgeItem">
                    <VerticalStackLayout Spacing="4" Padding="4">
                        <Label
                            Text="{Binding IconGlyph}"
                            FontFamily="MaterialSymbolsRounded"
                            FontSize="48"
                            TextColor="{StaticResource Primary}"
                            HorizontalOptions="Center"
                            HorizontalTextAlignment="Center" />
                        <Label
                            Text="{Binding Description}"
                            FontFamily="ShareTechMono"
                            FontSize="11"
                            TextColor="{StaticResource OnSurface}"
                            HorizontalOptions="Center"
                            HorizontalTextAlignment="Center"
                            LineBreakMode="WordWrap" />
                    </VerticalStackLayout>
                </DataTemplate>
            </CollectionView.ItemTemplate>
        </CollectionView>
    </Grid>
</ScrollView>
```

Note: `InverseBoolConverter` is already defined in the project (used on other pages). Confirm its key before using; add `xmlns:viewmodels` namespace declaration if not already present.

**Step 4.5 — DI Registration (MauiProgram.cs)**

Add after the existing `WeeklyHabitsPage` registrations:

```csharp
builder.Services.AddTransient<VaultViewModel>();
builder.Services.AddTransient<VaultPage>();
```

---

### Phase 5 — Tests (TDD_SPECIALIST)

**Step 5.1 — Domain: UserBadgeTests**

File: `tests/LifeGrid.Domain.Tests/UserProfile/UserBadgeTests.cs` (new)

```
UserBadge_Create_SetsAllProperties
```

**Step 5.2 — Domain: UserProfileTests (extend)**

File: `tests/LifeGrid.Domain.Tests/UserProfile/UserProfileTests.cs`

```
AwardBadge_AppendsToBadgesCollection
```

**Step 5.3 — Application: GetUserBadgesQueryTests**

File: `tests/LifeGrid.Application.Tests/Badge/GetUserBadgesQueryTests.cs` (new)

```
NullProfile_ReturnsEmptyCollection
ProfileWithNoBadges_ReturnsEmptyCollection
ProfileWithBadges_ReturnsMappedDtos
```

**Step 5.4 — Presentation stub**

File: `tests/LifeGrid.Application.Tests/Vault/VaultViewModelTests.cs` (stub comment only, same pattern as `TimelineViewModelTests.cs`)

---

## Test Count Summary

| Project                     | Before | New | After |
|-----------------------------|--------|-----|-------|
| LifeGrid.Domain.Tests       |     95 |   2 |    97 |
| LifeGrid.Application.Tests  |    137 |   3 |   140 |
| LifeGrid.Infrastructure.Tests |   66 |   0 |    66 |
| **Total**                   |  **298** | **5** | **303** |

---

## Implementation Notes (Post-Approval Corrections)

1. **InverseBool converter key** — Plan referenced `{StaticResource InverseBoolConverter}`; actual App.xaml key is `InverseBool`. Corrected in VaultPage.xaml.
2. **DataTemplate namespace** — Plan used `xmlns:viewmodels` + `viewmodels:VaultBadgeItem`; project convention is `xmlns:vm` + `vm:VaultBadgeItem`. Corrected to match GoalsPage standard.
3. **Build warning baseline** — `dotnet build` produces 12 pre-existing warnings (SQLite NU1903 + ViceSurveyPage `Device`/`NamedSize` obsolete API). Phase 25 adds zero new warnings.

---

## Clarification Decisions (captured)

| Question | Decision |
|----------|----------|
| Where does `IconName` live? | Domain entity (`UserBadge`) + EF migration |
| Locked/unearned badges? | Earned only; show empty state when none |
| Grid span | Dynamic: ≥ 400dp → 4 columns, else 3 |
| Badge awarding in scope? | No — display infrastructure only; awarding deferred |
