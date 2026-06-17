# Implementation Plan: Phase 2 — Onboarding State Engine & Step 1

**Status:** DONE — completed 2026-06-17  
**Requirements Source:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` (Phase 2 section, P2.1–P2.12)  
**Target:** All four projects — Domain, Application, Infrastructure, Presentation

---

## Key Decisions (from clarification)

| Decision | Choice |
|---|---|
| Setup page entry point | Dedicated `SetupPage` route — navigated to from HUD profile icon AND on cold start if `!IsComplete` |
| Auto-save timing | Debounced 500 ms via `CancellationTokenSource` in `SetupViewModel` |
| DB approach | Full EF Core + explicit migrations (TECHNICAL_STANDARDS.md compliance) |
| Dev escape hatch | None — accept Phase 2 constraint that tabs are always locked |
| Composition root | `Presentation.csproj` adds P2P ref to `Infrastructure` for DI wiring only |

---

## File Inventory

### New files

| File | Layer |
|---|---|
| `src/LifeGrid.Domain/Common/Result.cs` | Domain |
| `src/LifeGrid.Domain/Onboarding/OnboardingStep.cs` | Domain |
| `src/LifeGrid.Domain/Onboarding/OnboardingSession.cs` | Domain |
| `src/LifeGrid.Application/Onboarding/IOnboardingRepository.cs` | Application |
| `src/LifeGrid.Application/Onboarding/Queries/GetOrCreateOnboardingSessionQuery.cs` | Application |
| `src/LifeGrid.Application/Onboarding/Commands/UpdateGoalDraftCommand.cs` | Application |
| `src/LifeGrid.Application/Onboarding/Commands/CompleteStep1Command.cs` | Application |
| `src/LifeGrid.Infrastructure/Data/LifeGridDbContext.cs` | Infrastructure |
| `src/LifeGrid.Infrastructure/Data/EntityConfigurations/OnboardingSessionConfiguration.cs` | Infrastructure |
| `src/LifeGrid.Infrastructure/Data/Factories/LifeGridDbContextFactory.cs` | Infrastructure |
| `src/LifeGrid.Infrastructure/Data/Repositories/OnboardingRepository.cs` | Infrastructure |
| `src/LifeGrid.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` | Infrastructure |
| `src/LifeGrid.Infrastructure/Migrations/` *(generated)* | Infrastructure |
| `src/LifeGrid.Presentation/ViewModels/AppShellViewModel.cs` | Presentation |
| `src/LifeGrid.Presentation/ViewModels/SetupViewModel.cs` | Presentation |
| `src/LifeGrid.Presentation/Pages/SetupPage.xaml` + `.xaml.cs` | Presentation |
| `tests/LifeGrid.Domain.Tests/Onboarding/OnboardingSessionTests.cs` | Tests |
| `tests/LifeGrid.Application.Tests/Onboarding/GetOrCreateOnboardingSessionQueryHandlerTests.cs` | Tests |
| `tests/LifeGrid.Application.Tests/Onboarding/UpdateGoalDraftCommandHandlerTests.cs` | Tests |
| `tests/LifeGrid.Application.Tests/Onboarding/CompleteStep1CommandHandlerTests.cs` | Tests |
| `tests/LifeGrid.Infrastructure.Tests/Onboarding/OnboardingRepositoryTests.cs` | Tests |

### Modified files

| File | Change |
|---|---|
| `src/LifeGrid.Infrastructure/LifeGrid.Infrastructure.csproj` | Add `Microsoft.EntityFrameworkCore.Tools` |
| `src/LifeGrid.Presentation/LifeGrid.Presentation.csproj` | Add P2P ref to `LifeGrid.Infrastructure` |
| `src/LifeGrid.Presentation/MauiProgram.cs` | Add infrastructure DI + `context.Database.Migrate()` call |
| `src/LifeGrid.Presentation/App.xaml.cs` | Inject `AppShell` + `IMediator`; add `OnStart()` override |
| `src/LifeGrid.Presentation/AppShell.xaml` | Bind tab `IsEnabled` to `AppShellViewModel.IsOnboardingComplete` |
| `src/LifeGrid.Presentation/AppShell.xaml.cs` | Inject `AppShellViewModel`; register `setup` route |
| `src/LifeGrid.Presentation/Controls/HudView.xaml.cs` | `OnProfileTapped` → `Shell.Current.GoToAsync("setup")` |
| `tests/LifeGrid.Application.Tests/LifeGrid.Application.Tests.csproj` | Add P2P ref to `LifeGrid.Application` (if missing) |

---

## Phase A — Domain Layer (TDD: write tests first)

### Step A.1 — `Result<T>` Types
**File:** `src/LifeGrid.Domain/Common/Result.cs`

```csharp
namespace LifeGrid.Domain.Common;

public record Result(bool IsSuccess, string? Error = null)
{
    public static Result Success()              => new(true);
    public static Result Failure(string error) => new(false, error);
}

public record Result<T>(bool IsSuccess, T? Value = default, string? Error = null)
{
    public static Result<T> Success(T value)   => new(true, value);
    public static Result<T> Failure(string e)  => new(false, default, e);
}
```

### Step A.2 — `OnboardingStep` Enum
**File:** `src/LifeGrid.Domain/Onboarding/OnboardingStep.cs`

```csharp
namespace LifeGrid.Domain.Onboarding;

public enum OnboardingStep
{
    Unstarted,
    Step1_GoalDraftCaptured
}
```

### Step A.3 — `OnboardingSession` Entity
**File:** `src/LifeGrid.Domain/Onboarding/OnboardingSession.cs`

```csharp
namespace LifeGrid.Domain.Onboarding;

public sealed class OnboardingSession
{
    protected OnboardingSession() { }   // EF Core materialization

    public static OnboardingSession Create() => new()
    {
        SessionId            = Guid.NewGuid(),
        CurrentStep          = OnboardingStep.Unstarted,
        IsComplete           = false,
        RawGoalDraft         = null,
        LastActiveTimestamp  = DateTime.UtcNow
    };

    public Guid           SessionId           { get; private set; }
    public OnboardingStep CurrentStep         { get; private set; }
    public bool           IsComplete          { get; private set; }
    public string?        RawGoalDraft        { get; private set; }
    public DateTime       LastActiveTimestamp { get; private set; }

    public void UpdateDraft(string draft)
    {
        RawGoalDraft        = draft;
        LastActiveTimestamp = DateTime.UtcNow;
    }

    public void AdvanceToStep1()
    {
        CurrentStep         = OnboardingStep.Step1_GoalDraftCaptured;
        LastActiveTimestamp = DateTime.UtcNow;
    }
}
```

**EF Core note:** `protected` (not `private`) constructor is required so EF Core can instantiate the entity without bypassing the factory. Private-set properties are mapped via Fluent API.

### Step A.4 — Domain Tests (write before implementation)
**File:** `tests/LifeGrid.Domain.Tests/Onboarding/OnboardingSessionTests.cs`

Test cases (100% branch coverage):

| Test Name | Assertion |
|---|---|
| `Create_SetsUnstartedStep` | `CurrentStep == Unstarted` |
| `Create_SetsIsCompleteFalse` | `IsComplete == false` |
| `Create_SetsNullDraft` | `RawGoalDraft == null` |
| `Create_AssignsNewSessionId` | `SessionId != Guid.Empty` |
| `UpdateDraft_StoresDraftText` | `RawGoalDraft == "my goal"` |
| `UpdateDraft_UpdatesTimestamp` | `LastActiveTimestamp` advanced |
| `AdvanceToStep1_SetsStep1Captured` | `CurrentStep == Step1_GoalDraftCaptured` |
| `AdvanceToStep1_DoesNotSetComplete` | `IsComplete == false` (invariant guard) |

---

## Phase B — Application Layer

### Step B.1 — `IOnboardingRepository`
**File:** `src/LifeGrid.Application/Onboarding/IOnboardingRepository.cs`

```csharp
namespace LifeGrid.Application.Onboarding;

public interface IOnboardingRepository
{
    Task<OnboardingSession?> GetActiveSessionAsync(CancellationToken ct = default);
    Task<OnboardingSession>  UpsertAsync(OnboardingSession session, CancellationToken ct = default);
}
```

### Step B.2 — `GetOrCreateOnboardingSessionQuery` + Handler
**File:** `src/LifeGrid.Application/Onboarding/Queries/GetOrCreateOnboardingSessionQuery.cs`

```csharp
public record GetOrCreateOnboardingSessionQuery : IRequest<Result<OnboardingSession>>;
```

**Handler logic:**
1. Call `_repository.GetActiveSessionAsync()`.
2. If `null`: call `OnboardingSession.Create()`, persist via `UpsertAsync`, return `Result<OnboardingSession>.Success(session)`.
3. If found: return `Result<OnboardingSession>.Success(existingSession)`.

### Step B.3 — `UpdateGoalDraftCommand` + Handler
**File:** `src/LifeGrid.Application/Onboarding/Commands/UpdateGoalDraftCommand.cs`

```csharp
public record UpdateGoalDraftCommand(string Draft) : IRequest<Result>;
```

**Handler logic:**
1. `GetActiveSessionAsync()`.
2. If `null` → `Result.Failure("No active session")`.
3. Call `session.UpdateDraft(request.Draft)`.
4. `UpsertAsync(session)`.
5. Return `Result.Success()`.

### Step B.4 — `CompleteStep1Command` + Handler
**File:** `src/LifeGrid.Application/Onboarding/Commands/CompleteStep1Command.cs`

```csharp
public record CompleteStep1Command : IRequest<Result<OnboardingSession>>;
```

**Handler logic:**
1. `GetActiveSessionAsync()`.
2. If `null` → `Result<OnboardingSession>.Failure("No active session")`.
3. Call `session.AdvanceToStep1()`.
4. `UpsertAsync(session)`.
5. Return `Result<OnboardingSession>.Success(session)`.

### Step B.5 — Application Tests (write before implementation)

**`GetOrCreateOnboardingSessionQueryHandlerTests`:**

| Test | Setup | Assert |
|---|---|---|
| `NoExistingSession_CreatesAndReturnsNew` | `GetActiveSessionAsync` returns `null` | `UpsertAsync` called once; result `IsSuccess == true`; `CurrentStep == Unstarted` |
| `ExistingSession_ReturnsExisting` | `GetActiveSessionAsync` returns session | `UpsertAsync` NOT called; result value == existing session |

**`UpdateGoalDraftCommandHandlerTests`:**

| Test | Setup | Assert |
|---|---|---|
| `NoSession_ReturnsFailure` | `GetActiveSessionAsync` returns `null` | `IsSuccess == false` |
| `ExistingSession_SavesDraft` | session found | `UpsertAsync` called with `RawGoalDraft == "test"` |

**`CompleteStep1CommandHandlerTests`:**

| Test | Setup | Assert |
|---|---|---|
| `NoSession_ReturnsFailure` | `GetActiveSessionAsync` returns `null` | `IsSuccess == false` |
| `ExistingSession_AdvancesStep` | session found | returned session `CurrentStep == Step1_GoalDraftCaptured` |

---

## Phase C — Infrastructure Layer

### Step C.1 — `LifeGridDbContext`
**File:** `src/LifeGrid.Infrastructure/Data/LifeGridDbContext.cs`

```csharp
namespace LifeGrid.Infrastructure.Data;

public sealed class LifeGridDbContext(DbContextOptions<LifeGridDbContext> options)
    : DbContext(options)
{
    public DbSet<OnboardingSession> OnboardingSessions => Set<OnboardingSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(LifeGridDbContext).Assembly);
}
```

### Step C.2 — `OnboardingSessionConfiguration` (Fluent API)
**File:** `src/LifeGrid.Infrastructure/Data/EntityConfigurations/OnboardingSessionConfiguration.cs`

```csharp
public sealed class OnboardingSessionConfiguration
    : IEntityTypeConfiguration<OnboardingSession>
{
    public void Configure(EntityTypeBuilder<OnboardingSession> builder)
    {
        builder.ToTable("OnboardingProgressCache");
        builder.HasKey(e => e.SessionId);
        builder.Property(e => e.SessionId).ValueGeneratedNever();
        builder.Property(e => e.CurrentStep)
               .HasConversion<string>()    // store enum as string for readability
               .HasMaxLength(64);
        builder.Property(e => e.RawGoalDraft).HasMaxLength(2000);
        builder.Property(e => e.IsComplete);
        builder.Property(e => e.LastActiveTimestamp);
    }
}
```

### Step C.3 — `IDesignTimeDbContextFactory`
**File:** `src/LifeGrid.Infrastructure/Data/Factories/LifeGridDbContextFactory.cs`

```csharp
public sealed class LifeGridDbContextFactory : IDesignTimeDbContextFactory<LifeGridDbContext>
{
    public LifeGridDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LifeGridDbContext>()
            .UseSqlite("Data Source=lifegrid-dev.db")
            .Options;
        return new LifeGridDbContext(options);
    }
}
```

### Step C.4 — `OnboardingRepository`
**File:** `src/LifeGrid.Infrastructure/Data/Repositories/OnboardingRepository.cs`

```csharp
public sealed class OnboardingRepository(LifeGridDbContext db) : IOnboardingRepository
{
    public Task<OnboardingSession?> GetActiveSessionAsync(CancellationToken ct)
        => db.OnboardingSessions.FirstOrDefaultAsync(ct);

    public async Task<OnboardingSession> UpsertAsync(OnboardingSession session, CancellationToken ct)
    {
        var existing = await db.OnboardingSessions
            .FindAsync([session.SessionId], ct);
        if (existing is null)
            db.OnboardingSessions.Add(session);
        else
            db.Entry(existing).CurrentValues.SetValues(session);
        await db.SaveChangesAsync(ct);
        return session;
    }
}
```

### Step C.5 — `InfrastructureServiceExtensions`
**File:** `src/LifeGrid.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs`

```csharp
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<LifeGridDbContext>(opts =>
            opts.UseSqlite(connectionString));
        services.AddScoped<IOnboardingRepository, OnboardingRepository>();
        return services;
    }
}
```

### Step C.6 — EF Core Migration

Add `Microsoft.EntityFrameworkCore.Tools` to `LifeGrid.Infrastructure.csproj`:
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.9">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

Generate the migration (run from repo root):
```
dotnet ef migrations add InitialOnboardingSchema --project src/LifeGrid.Infrastructure
```

This produces `src/LifeGrid.Infrastructure/Migrations/` with the `InitialOnboardingSchema` files.

### Step C.7 — Infrastructure Tests
**File:** `tests/LifeGrid.Infrastructure.Tests/Onboarding/OnboardingRepositoryTests.cs`

Uses EF Core in-memory provider (already referenced as `Microsoft.EntityFrameworkCore.InMemory` in the test project).

| Test | Assertion |
|---|---|
| `GetActiveSession_EmptyDb_ReturnsNull` | `null` returned |
| `UpsertAsync_NewSession_PersistsAndReturns` | record found via `FindAsync` after upsert |
| `UpsertAsync_ExistingSession_UpdatesRecord` | second upsert with mutated draft → updated value in DB |

---

## Phase D — Presentation Layer

### Step D.1 — `LifeGrid.Presentation.csproj`: Add Infrastructure Reference

```xml
<ItemGroup>
  <ProjectReference Include="..\LifeGrid.Infrastructure\LifeGrid.Infrastructure.csproj" />
</ItemGroup>
```

**Rationale:** `MauiProgram.cs` is the composition root and must reference Infrastructure to call `services.AddInfrastructure(...)`. No Infrastructure types are imported in any ViewModel or Page.

### Step D.2 — `MauiProgram.cs`: DI Registration + Migrate

```csharp
// 1. Compute runtime SQLite path
var dbPath = Path.Combine(FileSystem.AppDataDirectory, "lifegrid.db");

// 2. Register Infrastructure services
builder.Services.AddInfrastructure($"Data Source={dbPath}");

// 3. Register MediatR handlers (Application + Infrastructure assemblies)
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(GetOrCreateOnboardingSessionQuery).Assembly);
});

// 4. Register MAUI presentation services
builder.Services.AddSingleton<AppShell>();
builder.Services.AddSingleton<AppShellViewModel>();
builder.Services.AddTransient<SetupPage>();
builder.Services.AddTransient<SetupViewModel>();

// 5. After Build(): run migrations before app starts
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LifeGridDbContext>();
    db.Database.Migrate();
}
return app;
```

### Step D.3 — `App.xaml.cs`: Inject Shell + Mediator

```csharp
public partial class App : Application
{
    private readonly AppShell  _shell;
    private readonly IMediator _mediator;

    public App(AppShell shell, IMediator mediator)
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;
        _shell   = shell;
        _mediator = mediator;
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new Window(_shell);

    protected override async void OnStart()
    {
        base.OnStart();
        var result = await _mediator.Send(new GetOrCreateOnboardingSessionQuery());
        if (result.IsSuccess && !result.Value!.IsComplete)
            await Shell.Current.GoToAsync("setup");
    }
}
```

### Step D.4 — `AppShellViewModel`
**File:** `src/LifeGrid.Presentation/ViewModels/AppShellViewModel.cs`

```csharp
[ObservableObject]
public partial class AppShellViewModel
{
    [ObservableProperty]
    private bool _isOnboardingComplete = false;
}
```

No logic here in Phase 2 (always false). In Phase 3 it will be updated when onboarding completes.

### Step D.5 — `AppShell.xaml`: Tab IsEnabled Binding

```xml
<Shell
    x:Class="LifeGrid.Presentation.AppShell"
    ...
    BindingContext="{x:Reference self}"
    x:Name="self">
```

Wait — BindingContext on Shell would interfere with page bindings. Use a different approach: set Shell's BindingContext to `AppShellViewModel` via code-behind only (see D.6), and bind tabs via `{Binding IsOnboardingComplete}`.

Each `Tab`:
```xml
<Tab Title="Home" IsEnabled="{Binding IsOnboardingComplete}">
```

(All four tabs get `IsEnabled="{Binding IsOnboardingComplete}"`)

### Step D.6 — `AppShell.xaml.cs`: Route Registration + ViewModel Binding

```csharp
public partial class AppShell : Shell
{
    public AppShell(AppShellViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        Routing.RegisterRoute("setup", typeof(SetupPage));
    }
}
```

### Step D.7 — `SetupViewModel`
**File:** `src/LifeGrid.Presentation/ViewModels/SetupViewModel.cs`

```csharp
[ObservableObject]
public partial class SetupViewModel(IMediator mediator)
{
    private CancellationTokenSource? _debounceCts;

    [ObservableProperty]
    private string _goalDraft = string.Empty;

    [ObservableProperty]
    private bool _isStep1Complete = false;

    public async Task LoadAsync()
    {
        var result = await mediator.Send(new GetOrCreateOnboardingSessionQuery());
        if (!result.IsSuccess) return;
        var session = result.Value!;
        GoalDraft      = session.RawGoalDraft ?? string.Empty;
        IsStep1Complete = session.CurrentStep == OnboardingStep.Step1_GoalDraftCaptured;
    }

    partial void OnGoalDraftChanged(string value) => ScheduleAutoSave(value);

    private void ScheduleAutoSave(string draft)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;
        Task.Delay(500, token).ContinueWith(
            async _ => await mediator.Send(new UpdateGoalDraftCommand(draft)),
            token,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);
    }

    [RelayCommand]
    private async Task CompleteStep1Async()
    {
        _debounceCts?.Cancel();
        await mediator.Send(new UpdateGoalDraftCommand(GoalDraft));
        var result = await mediator.Send(new CompleteStep1Command());
        if (result.IsSuccess)
            IsStep1Complete = true;
    }
}
```

### Step D.8 — `SetupPage.xaml` + Code-Behind
**File:** `src/LifeGrid.Presentation/Pages/SetupPage.xaml`

```xml
<ContentPage
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:controls="clr-namespace:LifeGrid.Presentation.Controls"
    xmlns:vm="clr-namespace:LifeGrid.Presentation.ViewModels"
    x:Class="LifeGrid.Presentation.Pages.SetupPage"
    Title="Profile Setup"
    BackgroundColor="{StaticResource Background}">

    <Grid RowDefinitions="*, 50">

        <!-- Main content area -->
        <ScrollView Grid.Row="0">
            <VerticalStackLayout Padding="16" Spacing="16" VerticalOptions="Center">

                <!-- State A: Unstarted -->
                <VerticalStackLayout
                    IsVisible="{Binding IsStep1Complete, Converter={StaticResource InverseBool}}"
                    Spacing="16">
                    <Label
                        Text="What's your main goal?"
                        Style="{StaticResource HeadlineStyle}" />
                    <Entry
                        Text="{Binding GoalDraft}"
                        Placeholder="Describe your goal..."
                        FontFamily="ShareTechMono-Regular"
                        TextColor="{StaticResource OnSurface}"
                        BackgroundColor="{StaticResource Surface}" />
                    <Button
                        Text="Next"
                        Command="{Binding CompleteStep1Command}"
                        CornerRadius="2"
                        BackgroundColor="{StaticResource Primary}"
                        TextColor="{StaticResource OnPrimary}" />
                </VerticalStackLayout>

                <!-- State B: Step1_GoalDraftCaptured -->
                <Label
                    IsVisible="{Binding IsStep1Complete}"
                    Text="Step 1 Complete. Awaiting Phase 3 setup steps."
                    Style="{StaticResource HeadlineStyle}"
                    HorizontalOptions="Center"
                    HorizontalTextAlignment="Center" />

            </VerticalStackLayout>
        </ScrollView>

        <!-- Ad banner -->
        <controls:AdBannerView Grid.Row="1" />

    </Grid>
</ContentPage>
```

**Code-behind:**
```csharp
public partial class SetupPage : ContentPage
{
    private readonly SetupViewModel _vm;

    public SetupPage(SetupViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
```

**Note:** Requires an `InverseBoolConverter` resource. Add it to `Resources/Styles/Styles.xaml`:
```xml
<converters:InverseBoolConverter x:Key="InverseBool" />
```
And create `src/LifeGrid.Presentation/Converters/InverseBoolConverter.cs`:
```csharp
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
        => value is bool b && !b;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c)
        => value is bool b && !b;
}
```

### Step D.9 — `HudView.xaml.cs`: Profile Tap Navigation

```csharp
private async void OnProfileTapped(object sender, TappedEventArgs e)
    => await Shell.Current.GoToAsync("setup");
```

---

## Phase E — Build & Test Verification

### Step E.1 — Generate EF Core Migration

```
dotnet ef migrations add InitialOnboardingSchema --project src/LifeGrid.Infrastructure
```

Expected output: `Migrations/` folder with `*_InitialOnboardingSchema.cs` and snapshot file.

### Step E.2 — Build Verification

```
dotnet build LifeGrid.slnx
```

Exit code 0, 0 errors, 0 warnings.

### Step E.3 — Test Verification

```
dotnet test
```

All tests pass. New test classes produce 100% branch coverage for:
- `OnboardingSession` state transitions
- All three MediatR handlers (null-session and happy paths)
- `OnboardingRepository` (empty DB, insert, update)

---

## Dependency Graph for This Phase

```
MauiProgram.cs
  ├── AddInfrastructure("Data Source={path}/lifegrid.db")
  │     └── LifeGridDbContext (EF Core + SQLite)
  │         └── Migrate() called before App starts
  ├── AddMediatR → handlers in LifeGrid.Application
  ├── AddSingleton<AppShell>
  ├── AddSingleton<AppShellViewModel>
  ├── AddTransient<SetupPage>
  └── AddTransient<SetupViewModel>

App.OnStart()
  └── GetOrCreateOnboardingSessionQuery
        → if !IsComplete → Shell.GoToAsync("setup")

AppShell.xaml
  ├── BindingContext = AppShellViewModel
  ├── Tab[0..3].IsEnabled = {Binding IsOnboardingComplete}  (always false Phase 2)
  └── Routing.RegisterRoute("setup", typeof(SetupPage))

SetupPage
  ├── OnAppearing → SetupViewModel.LoadAsync()
  │     └── GetOrCreateOnboardingSessionQuery
  ├── Entry.Text ↔ SetupViewModel.GoalDraft
  │     └── OnGoalDraftChanged → debounce 500ms → UpdateGoalDraftCommand
  └── Button.Command = CompleteStep1Command
        └── UpdateGoalDraftCommand (immediate) → CompleteStep1Command
              └── OnboardingSession.AdvanceToStep1()
                    → IsStep1Complete = true → UI swaps to "Step 1 Complete"

HudView.OnProfileTapped → Shell.GoToAsync("setup")
```

---

## Risk Notes

| Risk | Mitigation |
|---|---|
| `Tab.IsEnabled` binding may not propagate in all MAUI Shell versions | If binding fails, set `IsEnabled` programmatically in `AppShell.xaml.cs` constructor after `InitializeComponent()` |
| `context.Database.Migrate()` on Android first launch may block UI thread briefly | Call is synchronous but migration is tiny (single table). If perceptible, wrap in `Task.Run()` |
| `Shell.Current` is null during `OnStart()` race condition | `OnStart()` fires after `CreateWindow()` returns and Shell is set as `MainPage` — `Shell.Current` is safe |
| EF Core `protected` constructor: EF 10 uses reflection to call it; `sealed` class + `protected` ctor must be allowed | EF Core supports this pattern — verified in EF Core docs for private/protected constructors via Fluent API |
| `FileSystem.AppDataDirectory` unavailable at `builder.Services` registration time | `FileSystem.AppDataDirectory` is available once MAUI platform is initialized — it is safe in `CreateMauiApp()` body after `MauiApp.CreateBuilder()` |
| `BindingContext = AppShellViewModel` on Shell may conflict with child page bindings | Shell `BindingContext` does NOT propagate to `ShellContent` pages in MAUI — pages set their own `BindingContext` independently |
