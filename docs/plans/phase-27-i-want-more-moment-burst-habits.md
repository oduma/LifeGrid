# Phase 27 — "I Want More": AI-Generated Moment Burst Habits
Status: DONE

---

## Pre-Flight Analysis (LEAD_ARCHITECT)

### Bounded Context: Habit / Gamification / Presentation

Phase 27 spans 5 concerns, executed in strict dependency order:
1. **Domain** — `GamificationCalculationEngine.CalculateEntryReward` gains 3× multiplier for `MomentBurst` (the `_ = habitType` stub removed).
2. **Application** — `IGeminiMomentBurstService` interface, `MomentBurstResult` / `MomentBurstOutcome` DUs, `RequestMomentBurstCommand` handler.
3. **Infrastructure** — `GeminiMomentBurstService` implementation (Prompt5 embedded), DI registration.
4. **Presentation** — `WeeklyGoalGroupItem` + `WeeklyHabitItem` ViewModel additions, `IWantMoreCommand` on `WeeklyHabitsViewModel` and `HomeViewModel`, XAML changes to both pages.
5. **Tests** — Domain (+3), Application (+4), Infrastructure (+1) = **+8 total → 323**.

### Key Architecture Decisions

- `RequestMomentBurstCommand` handler lives in Application; it is the single writer for `Habit` rows in this path.
- `IGeminiMomentBurstService` is declared in Application (Zero-Dependency Rule); only Infrastructure knows about `IChatClient` or HTTP.
- The command accepts `WeekId` + `WeekGoalId` so the handler can load the `Week` entity (which carries `WeekGoals`) via a single `GetByIdAsync` call — no new repository method needed.
- No EF migration. `HabitType.MomentBurst` and the `Habits` table are pre-existing.
- Triple XP/SP multiplier is applied in `CalculateEntryReward` via an `int multiplier = habitType == HabitType.MomentBurst ? 3 : 1` guard. Flash multiplier remains deferred.

### Clarification Decisions (captured)

| Question | Decision |
|----------|----------|
| Button placement (multi-goal) | Per goal-group header; enabled when **that specific goal** has GP ≥ 100% |
| Home Dashboard placement | Inline in each goal's group, same treatment as WeeklyHabitsPage |
| Triple XP/SP | Implement 3× multiplier in `CalculateEntryReward` for `MomentBurst` in Phase 27 |
| Prompt5 habits payload | Pass only habits belonging to the tapped goal's `WeekGoalId` |

---

## Implementation Plan

### Phase 1 — Domain: Gamification Engine (GAMIFICATION_SPECIALIST)

**Step 1.1 — Update `GamificationCalculationEngine.CalculateEntryReward`**

File: `src/LifeGrid.Domain/Gamification/GamificationCalculationEngine.cs` (modify)

Replace the `_ = habitType;` stub with a multiplier guard:

```csharp
public static EntryReward CalculateEntryReward(
    HabitType habitType, double actualValue, double targetValue, bool hasProof)
{
    var tier       = DetermineProofTier(actualValue, targetValue, hasProof);
    int multiplier = habitType == HabitType.MomentBurst ? 3 : 1;
    return tier switch
    {
        ProofTier.Proven          => new EntryReward(20 * multiplier, 4 * multiplier),
        ProofTier.PartiallyProven => new EntryReward(10 * multiplier, 2 * multiplier),
        _                         => new EntryReward( 3 * multiplier, 1 * multiplier)
    };
}
```

No other domain changes. `HabitType.MomentBurst` and `CalculateWeekGoalGp` exclusion are pre-existing.

---

### Phase 2 — Application Layer (SENIOR_NET_DEVELOPER)

**Step 2.1 — `MomentBurstResult` discriminated union**

New file: `src/LifeGrid.Application/MomentBurst/MomentBurstResult.cs`

```csharp
namespace LifeGrid.Application.MomentBurst;

public abstract record MomentBurstResult
{
    public sealed record Accepted(
        string QuestName,
        string Description,
        double MeasureValue,
        string MeasureUnit) : MomentBurstResult;

    public sealed record Denied(string Message) : MomentBurstResult;
}
```

**Step 2.2 — `MomentBurstOutcome` discriminated union**

New file: `src/LifeGrid.Application/MomentBurst/MomentBurstOutcome.cs`

```csharp
using LifeGrid.Application.WeeklyHabits;

namespace LifeGrid.Application.MomentBurst;

public abstract record MomentBurstOutcome
{
    public sealed record HabitCreated(WeeklyHabitItemDto NewHabit) : MomentBurstOutcome;
    public sealed record Denied(string Message) : MomentBurstOutcome;
}
```

**Step 2.3 — `IGeminiMomentBurstService`**

New file: `src/LifeGrid.Application/MomentBurst/IGeminiMomentBurstService.cs`

```csharp
using LifeGrid.Domain.Common;

namespace LifeGrid.Application.MomentBurst;

public interface IGeminiMomentBurstService
{
    Task<Result<MomentBurstResult>> GenerateAsync(
        string            userFreeText,
        string            weeklyHabitsJson,
        DateTime          currentDate,
        CancellationToken ct = default);
}
```

**Step 2.4 — `RequestMomentBurstCommand` + Handler**

New file: `src/LifeGrid.Application/MomentBurst/RequestMomentBurstCommand.cs`

```csharp
public record RequestMomentBurstCommand(
    Guid   WeekId,
    Guid   WeekGoalId,
    string UserFreeText)
    : IRequest<Result<MomentBurstOutcome>>;

public sealed class RequestMomentBurstCommandHandler(
    IWeekRepository           weekRepository,
    IHabitRepository          habitRepository,
    IGeminiMomentBurstService momentBurstService,
    IDateTimeProvider         clock,
    IUnitOfWork               unitOfWork)
    : IRequestHandler<RequestMomentBurstCommand, Result<MomentBurstOutcome>>
```

Handler logic:

```
1. var week = await weekRepository.GetByIdAsync(command.WeekId, ct)
   if null → Result.Failure("Week not found.")

2. var weekGoal = week.WeekGoals.FirstOrDefault(wg => wg.WeekGoalId == command.WeekGoalId)
   if null → Result.Failure("WeekGoal not found.")

3. Compute currentMonday: today.AddDays(-((int)today.DayOfWeek - (int)Monday + 7) % 7)
   if week.StartDate.Date != currentMonday → Result.Failure("Not the current week.")

4. if weekGoal.GoalWeeklyGp < 100.0 → Result.Failure("Goal progress must be 100% to request a Moment Burst.")

5. var habits = await habitRepository.GetByWeekGoalIdAsync(command.WeekGoalId, ct)

6. Build habitsJson: JSON array of each habit's name, target, and cumulative completed value.
   Use System.Text.Json.JsonSerializer.

7. var aiResult = await momentBurstService.GenerateAsync(command.UserFreeText, habitsJson, clock.UtcNow.Date, ct)
   if !aiResult.IsSuccess → Result.Failure(aiResult.Error!)

8. if aiResult.Value is MomentBurstResult.Denied denied
   → Result.Success(MomentBurstOutcome.Denied(denied.Message))    // no DB writes

9. var accepted = (MomentBurstResult.Accepted)aiResult.Value!
   var deadline  = DateTime.SpecifyKind(week.StartDate.AddDays(6), DateTimeKind.Utc)
   var habit     = Habit.Create(weekGoalId, HabitType.MomentBurst,
                                accepted.QuestName, accepted.Description,
                                accepted.MeasureValue, accepted.MeasureUnit, deadline)

10. await habitRepository.AddRangeAsync([habit], ct)
    await unitOfWork.CommitAsync(ct)

11. var itemDto = new WeeklyHabitItemDto(habit.HabitId, habit.HabitType.ToString(),
                                         habit.HabitName, habit.HabitDescription,
                                         habit.TargetValue, habit.MeasurementUnit,
                                         habit.DeadlineDateTime, [])
    return Result.Success(MomentBurstOutcome.HabitCreated(itemDto))
```

---

### Phase 3 — Infrastructure Layer (SENIOR_NET_DEVELOPER + AI_INTEGRATION_ENGINEER)

**Step 3.1 — Embedded prompt file**

New file: `src/LifeGrid.Infrastructure/AI/Prompts/prompt5.txt`

Based on `docs/specs/assets/prompts/prompt5.txt`. Augment the JSON output schema with a `status` field:

```
{
  "status": "accepted",
  "momentum_burst_quest_name": "Quest name here",
  "habit_description": "Clear description of the extra task.",
  "measure": {
    "value": 1.0,
    "unit": "Unit of measurement"
  }
}
```

For denial case, the AI sets `"status": "denied"` and uses the description field for the redirect message.

**Step 3.2 — `GeminiMomentBurstService`**

New file: `src/LifeGrid.Infrastructure/AI/GeminiMomentBurstService.cs`

```csharp
internal sealed class GeminiMomentBurstService(IChatClient chatClient)
    : IGeminiMomentBurstService
{
    private static readonly string PromptTemplate = LoadEmbeddedPrompt("prompt5.txt");

    public async Task<Result<MomentBurstResult>> GenerateAsync(
        string userFreeText, string weeklyHabitsJson, DateTime currentDate, CancellationToken ct = default)
    {
        var prompt = PromptTemplate
            .Replace("${CURRENT_DATE}",       currentDate.ToString("MMMM d, yyyy"))
            .Replace("${USER_FREE_TEXT}",      userFreeText)
            .Replace("${WEEKLY_HABITS_JSON}",  weeklyHabitsJson);

        string raw;
        try
        {
            var response = await chatClient.GetResponseAsync(
                new List<ChatMessage> { new(ChatRole.User, prompt) }, ct);
            raw = response.Text ?? string.Empty;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        { return Result<MomentBurstResult>.Failure(ex.Message); }
        catch (Exception ex)
        { return Result<MomentBurstResult>.Failure($"Gemini request failed: {ex.Message}"); }

        return ParseResponse(StripCodeFences(raw));
    }

    private static Result<MomentBurstResult> ParseResponse(string json)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var       root = doc.RootElement;

            var status = root.TryGetProperty("status", out var sp)
                ? sp.GetString() ?? string.Empty
                : string.Empty;

            if (status == "denied")
            {
                var msg = root.TryGetProperty("habit_description", out var dp)
                    ? dp.GetString() ?? "Stay focused on your current habits."
                    : "Stay focused on your current habits.";
                return Result<MomentBurstResult>.Success(new MomentBurstResult.Denied(msg));
            }

            var name  = root.GetProperty("momentum_burst_quest_name").GetString() ?? string.Empty;
            var desc  = root.GetProperty("habit_description").GetString()          ?? string.Empty;
            var meas  = root.GetProperty("measure");
            var value = meas.GetProperty("value").GetDouble();
            var unit  = meas.GetProperty("unit").GetString() ?? string.Empty;

            return Result<MomentBurstResult>.Success(
                new MomentBurstResult.Accepted(name, desc, value, unit));
        }
        catch (JsonException ex)
        {
            return Result<MomentBurstResult>.Failure($"Gemini returned malformed JSON: {ex.Message}");
        }
    }

    // LoadEmbeddedPrompt and StripCodeFences copied from GeminiHabitGenerationService pattern
}
```

**Step 3.3 — `LifeGrid.Infrastructure.csproj`**

Add embedded resource entry:
```xml
<EmbeddedResource Include="AI\Prompts\prompt5.txt" />
```

**Step 3.4 — `InfrastructureServiceExtensions`**

Add inside `AddInfrastructure(...)`:
```csharp
services.AddScoped<IGeminiMomentBurstService, GeminiMomentBurstService>();
```

---

### Phase 4 — Presentation Layer (MAUI_UX_ENGINEER)

**Step 4.1 — `WeeklyGoalGroupItem.cs`**

File: `src/LifeGrid.Presentation/ViewModels/WeeklyGoalGroupItem.cs` (modify)

Add constructor parameter `bool isCurrentWeek = false`.

New properties:
```csharp
public Guid WeekGoalId          { get; }  // = dto.GoalId (used as WeekGoalId in command — see Note below)
public bool CanRequestMomentBurst { get; } // = isCurrentWeek && dto.GoalWeeklyGp >= 100.0
```

> **Note on WeekGoalId vs GoalId:** `WeeklyGoalGroupDto` does not currently carry `WeekGoalId`. The constructor must be updated to receive it (or `WeeklyGoalGroupDto` must be extended). The cleanest fix: add `WeekGoalId` to `WeeklyGoalGroupDto` in `WeeklyHabitsDashboardDto.cs`, then populate it from `wg.WeekGoalId` in `GetWeeklyHabitsQueryHandler` and `GetCurrentWeekHabitsQueryHandler`.

**Step 4.2 — `WeeklyHabitsDashboardDto.cs`**

File: `src/LifeGrid.Application/WeeklyHabits/WeeklyHabitsDashboardDto.cs` (modify)

Add `WeekGoalId` to `WeeklyGoalGroupDto`:
```csharp
public record WeeklyGoalGroupDto(
    Guid   WeekGoalId,      // ← new first field
    Guid   GoalId,
    string GoalDescription,
    ...);
```

Update both query handlers (`GetWeeklyHabitsQueryHandler` and `GetCurrentWeekHabitsQueryHandler`) to pass `wg.WeekGoalId` as the first argument when constructing `WeeklyGoalGroupDto`.

**Step 4.3 — `WeeklyHabitItem.cs`**

File: `src/LifeGrid.Presentation/ViewModels/WeeklyHabitItem.cs` (modify)

Add computed property:
```csharp
public bool IsMomentBurst => HabitTypeLabel == "MomentBurst";
```

**Step 4.4 — `WeeklyHabitsViewModel.cs`**

File: `src/LifeGrid.Presentation/ViewModels/WeeklyHabitsViewModel.cs` (modify)

In `LoadAsync()`, compute current week flag:
```csharp
var today         = DateTime.Today;
int daysFromMon   = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
var currentMonday = today.AddDays(-daysFromMon);
var isCurrentWeek = dto.StartDate.Date == currentMonday;

foreach (var g in dto.GoalGroups)
    GoalGroups.Add(new WeeklyGoalGroupItem(g, isFuture, isCurrentWeek));
```

Add command:
```csharp
[RelayCommand]
private async Task IWantMoreAsync(WeeklyGoalGroupItem item)
{
    var userInput = await Shell.Current.CurrentPage.DisplayPromptAsync(
        "I Want More", "What are you looking for?");
    if (string.IsNullOrWhiteSpace(userInput)) return;

    var result = await _mediator.Send(
        new RequestMomentBurstCommand(_weekId, item.WeekGoalId, userInput));
    if (!result.IsSuccess) return;

    if (result.Value is MomentBurstOutcome.Denied denied)
    {
        await Shell.Current.CurrentPage.DisplayAlert("Keep the Focus", denied.Message, "OK");
        return;
    }

    await LoadAsync();
}
```

**Step 4.5 — `HomeViewModel.cs`**

File: `src/LifeGrid.Presentation/ViewModels/HomeViewModel.cs` (modify)

Store `_currentWeekId` (loaded from `GetCurrentWeekHabitsQuery` response).

In `LoadAsync()`:
```csharp
_currentWeekId = dto.WeekId;
foreach (var g in dto.GoalGroups)
    GoalGroups.Add(new WeeklyGoalGroupItem(g, isFuture: false, isCurrentWeek: true));
```

Add command (same pattern as WeeklyHabitsViewModel):
```csharp
[RelayCommand]
private async Task IWantMoreAsync(WeeklyGoalGroupItem item)
{
    var userInput = await Shell.Current.CurrentPage.DisplayPromptAsync(
        "I Want More", "What are you looking for?");
    if (string.IsNullOrWhiteSpace(userInput)) return;

    var result = await _mediator.Send(
        new RequestMomentBurstCommand(_currentWeekId, item.WeekGoalId, userInput));
    if (!result.IsSuccess) return;

    if (result.Value is MomentBurstOutcome.Denied denied)
    {
        await Shell.Current.CurrentPage.DisplayAlert("Keep the Focus", denied.Message, "OK");
        return;
    }

    await LoadAsync();
}
```

**Step 4.6 — `WeeklyHabitsPage.xaml`**

File: `src/LifeGrid.Presentation/Pages/WeeklyHabitsPage.xaml` (modify)

**A) Per-goal-group: "I Want More" button** — insert after the habit `VerticalStackLayout` inside `DataTemplate x:DataType="vm:WeeklyGoalGroupItem"`:

```xml
<Button
    Margin="0,4,0,0"
    Padding="10,6"
    Text="I Want More"
    FontFamily="{StaticResource FontShareTechMono}"
    FontSize="12"
    TextColor="{StaticResource Primary}"
    BackgroundColor="Transparent"
    BorderColor="{StaticResource Primary}"
    BorderWidth="1"
    CornerRadius="2"
    IsVisible="{Binding CanRequestMomentBurst}"
    Command="{Binding BindingContext.IWantMoreCommand, Source={x:Reference PageRoot}}"
    CommandParameter="{Binding .}" />
```

**B) Moment Burst habit card visual treatment** — inside `DataTemplate x:DataType="vm:WeeklyHabitItem"`:

1. Add `DataTrigger` on the `Border` for Primary stroke:
```xml
<DataTrigger TargetType="Border" Binding="{Binding IsMomentBurst}" Value="True">
    <Setter Property="Stroke"          Value="{StaticResource Primary}" />
    <Setter Property="StrokeThickness" Value="2" />
</DataTrigger>
```

2. Replace the existing `Grid ColumnDefinitions="Auto,*"` type-pill + name row with a conditional structure: when `IsMomentBurst`, show the "MOMENT BURST" header and icon; otherwise show the standard type pill. Use two `Grid` elements toggled via `IsVisible="{Binding IsMomentBurst}"` / `IsVisible="{Binding IsNotMomentBurst}"`.

   Add `public bool IsNotMomentBurst => !IsMomentBurst;` to `WeeklyHabitItem`.

   Moment Burst header row:
   ```xml
   <Grid ColumnDefinitions="Auto,*" ColumnSpacing="6" IsVisible="{Binding IsMomentBurst}">
       <Label
           Grid.Column="0"
           Text="&#xE3B4;"
           FontFamily="MaterialSymbolsRounded"
           FontSize="16"
           TextColor="{StaticResource Primary}"
           VerticalOptions="Center" />
       <Label
           Grid.Column="1"
           Text="MOMENT BURST"
           FontFamily="{StaticResource FontShareTechMono}"
           FontSize="9"
           TextColor="{StaticResource Primary}"
           VerticalOptions="Center" />
   </Grid>
   ```

   Standard type-pill row (add `IsVisible="{Binding IsNotMomentBurst}"`):
   ```xml
   <Grid ColumnDefinitions="Auto,*" ColumnSpacing="6" IsVisible="{Binding IsNotMomentBurst}">
       <!-- existing pill + name content unchanged -->
   </Grid>
   ```

   The habit name `Label` remains outside both grids at its own row (or stays inside the second grid — keep consistent with current layout).

**Step 4.7 — `HomePage.xaml`**

File: `src/LifeGrid.Presentation/Pages/HomePage.xaml` (modify)

Apply the exact same changes as Step 4.6: the "I Want More" button and the Moment Burst card visual treatment are duplicated in both pages (same `DataTemplate` patterns, same `x:Reference PageRoot` bindings, different command name: `IWantMoreCommand`).

**Step 4.8 — `MauiProgram.cs`**

No changes required. `IGeminiMomentBurstService` is registered in `InfrastructureServiceExtensions` (Step 3.4). `RequestMomentBurstCommand` is handled via MediatR's auto-discovery (handler is `public sealed class` in the same assembly as the interface registration).

---

### Phase 5 — Tests (TDD_SPECIALIST)

**Step 5.1 — Domain: `GamificationCalculationEngineTests.cs`** (modify — add 3 tests)

File: `tests/LifeGrid.Domain.Tests/Gamification/GamificationCalculationEngineTests.cs`

```
MomentBurst_Proven_Returns60Xp12Sp
MomentBurst_PartiallyProven_Returns30Xp6Sp
MomentBurst_Unproven_Returns9Xp3Sp
```

**Step 5.2 — Application: `RequestMomentBurstCommandTests.cs`** (new — 4 tests)

File: `tests/LifeGrid.Application.Tests/MomentBurst/RequestMomentBurstCommandTests.cs`

Mocks: `IWeekRepository`, `IHabitRepository`, `IGeminiMomentBurstService`, `IDateTimeProvider`, `IUnitOfWork`

```
AiDenied_NoHabitInserted_ReturnsDeniedOutcome
  → service returns Denied("Finish your core habits first.")
  → habitRepository.AddRangeAsync NOT called
  → outcome is MomentBurstOutcome.Denied

AiAccepted_InsertsHabitWithMomentBurstType
  → service returns Accepted("Quest", "Desc", 1.0, "reps")
  → habitRepository.AddRangeAsync called once with habit whose HabitType == MomentBurst

AiAccepted_WeekGoalIdLinkedCorrectly
  → service returns Accepted
  → inserted habit's WeekGoalId == command.WeekGoalId

GpBelow100_ReturnsFailure
  → weekGoal.GoalWeeklyGp = 85.0
  → handler returns Result.Failure (not calling service at all)
```

**Step 5.3 — Infrastructure: `MomentBurstHabitPersistenceTests.cs`** (new — 1 test)

File: `tests/LifeGrid.Infrastructure.Tests/MomentBurst/MomentBurstHabitPersistenceTests.cs`

```
MomentBurstHabit_PersistedWithHabitTypeMomentBurst
  → create Habit.Create(weekGoalId, HabitType.MomentBurst, ...)
  → AddRangeAsync + CommitAsync
  → GetByWeekGoalIdAsync returns 1 habit
  → stored.HabitType == HabitType.MomentBurst
```

---

## Test Count Summary

| Layer | Baseline | New | Total |
|---|---|---|---|
| Domain | 97 | +3 | 100 |
| Application | 151 | +4 | 155 |
| Infrastructure | 67 | +1 | 68 |
| **Total** | **315** | **+8** | **323** |

---

## File Change Summary

| File | Action |
|---|---|
| `src/LifeGrid.Domain/Gamification/GamificationCalculationEngine.cs` | Modify |
| `src/LifeGrid.Application/MomentBurst/MomentBurstResult.cs` | New |
| `src/LifeGrid.Application/MomentBurst/MomentBurstOutcome.cs` | New |
| `src/LifeGrid.Application/MomentBurst/IGeminiMomentBurstService.cs` | New |
| `src/LifeGrid.Application/MomentBurst/RequestMomentBurstCommand.cs` | New |
| `src/LifeGrid.Application/WeeklyHabits/WeeklyHabitsDashboardDto.cs` | Modify (+WeekGoalId to WeeklyGoalGroupDto) |
| `src/LifeGrid.Application/WeeklyHabits/GetWeeklyHabitsQueryHandler.cs` | Modify (pass WeekGoalId) |
| `src/LifeGrid.Application/Home/GetCurrentWeekHabitsQueryHandler.cs` | Modify (pass WeekGoalId) |
| `src/LifeGrid.Infrastructure/AI/Prompts/prompt5.txt` | New |
| `src/LifeGrid.Infrastructure/AI/GeminiMomentBurstService.cs` | New |
| `src/LifeGrid.Infrastructure/LifeGrid.Infrastructure.csproj` | Modify (+EmbeddedResource) |
| `src/LifeGrid.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` | Modify (+DI) |
| `src/LifeGrid.Presentation/ViewModels/WeeklyGoalGroupItem.cs` | Modify (+WeekGoalId, +CanRequestMomentBurst, +isCurrentWeek param) |
| `src/LifeGrid.Presentation/ViewModels/WeeklyHabitItem.cs` | Modify (+IsMomentBurst, +IsNotMomentBurst) |
| `src/LifeGrid.Presentation/ViewModels/WeeklyHabitsViewModel.cs` | Modify (+IWantMoreCommand, isCurrentWeek calc) |
| `src/LifeGrid.Presentation/ViewModels/HomeViewModel.cs` | Modify (+IWantMoreCommand, _currentWeekId) |
| `src/LifeGrid.Presentation/Pages/WeeklyHabitsPage.xaml` | Modify (button + card visual treatment) |
| `src/LifeGrid.Presentation/Pages/HomePage.xaml` | Modify (button + card visual treatment) |
| `tests/LifeGrid.Domain.Tests/Gamification/GamificationCalculationEngineTests.cs` | Modify (+3 tests) |
| `tests/LifeGrid.Application.Tests/MomentBurst/RequestMomentBurstCommandTests.cs` | New |
| `tests/LifeGrid.Infrastructure.Tests/MomentBurst/MomentBurstHabitPersistenceTests.cs` | New |
