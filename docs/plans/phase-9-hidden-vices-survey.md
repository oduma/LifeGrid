# Phase 9 — Hidden Vices Survey & Analysis Pipeline
## Implementation Plan

**Status:** DONE — completed 2026-06-19  
**Requirements source:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` §Phase 9  
**Actual new tests:** 36 (10 Domain + 14 Application + 12 Infrastructure)  
**Total tests after phase:** 193 (71 Domain + 64 Application + 58 Infrastructure)  

---

## Clarifications (agreed 2026-06-19)

| # | Question | Answer |
|---|---|---|
| Q1 | Entry point scope | `UserSetupPage` `DetectHiddenVicesCommand` stub only; Interaction Hub entry deferred |
| Q2 | Goal injection for Loop 1 | All active goals passed as JSON context to prompt3.1 |
| Q3 | Completion screen | Single `ViceSurveyPage`; state machine (Loading → Questions → Analyzing → Complete) |
| Q4 | Button labels | "Next" / "Analyze Profile" (from requirements doc, not wireframe text) |

---

## Pre-Flight Architecture Review

### Existing Assets That Phase 9 Builds On
- `Goal.LinkedBadHabits` collection + `LinkedBadHabit` value object already exist (Phase 5). The `GoalLinkedBadHabits` EF table already exists. **No new migration needed for vices.**
- `UserEconomy.MaxShieldCap` is a persisted property in `UserEconomy` table. **Adding the cap-3 expansion is a pure domain method.**
- `GoalRepository.GetAllByUserIdAsync` returns EF-tracked entities (no `.AsNoTracking()`). Mutating them + calling `IUnitOfWork.CommitAsync()` persists changes — no `UpdateAsync` method needed.
- `UserSetupPage.xaml` already has `<Button Text="Detect Hidden Vices" Command="{Binding DetectHiddenVicesCommand}" />` wired. **Only the ViewModel stub needs replacing.**
- `InfrastructureServiceExtensions.cs` is the single DI registration point for all Gemini services.
- Prompt files live at `src/LifeGrid.Infrastructure/AI/Prompts/` and must be declared `<EmbeddedResource>` in `LifeGrid.Infrastructure.csproj`. Accessed via `LifeGrid.Infrastructure.AI.Prompts.{fileName}` resource name.

### New Migrations Required
One EF Core migration: `Phase9_AddViceSurveyCompleted` — adds `IsViceSurveyCompleted INTEGER NOT NULL DEFAULT 0` to the `UserProfiles` table.

---

## Phase A — Domain Layer (TDD First)

> **Persona:** `SENIOR_NET_DEVELOPER.md` + `TDD_SPECIALIST.md`  
> **Constraint:** Zero-Dependency Rule — pure C# only. No MAUI, EF, or NuGet in Domain.

### A1. Write Domain Tests First

**File:** `tests/LifeGrid.Domain.Tests/Vice/UserProfileViceSurveyTests.cs`

| Test | Assertion |
|---|---|
| `GrantSurveyBonusShield_SetsIsViceSurveyCompleted` | `profile.IsViceSurveyCompleted == true` |
| `GrantSurveyBonusShield_ExpandsCapToThree` | `profile.Economy.MaxShieldCap == 3` |
| `GrantSurveyBonusShield_GrantsOneShield` | `profile.Economy.ShieldsAvailable == 1` |
| `GrantSurveyBonusShield_Idempotent_DoesNotDoubleGrant` | Calling twice: `ShieldsAvailable` stays 1 (no-op second call) |

**File:** `tests/LifeGrid.Domain.Tests/Vice/GoalLinkedBadHabitsTests.cs`

| Test | Assertion |
|---|---|
| `SetLinkedBadHabits_ReplacesCollection` | Call with 2 items → `LinkedBadHabits.Count == 2` |
| `SetLinkedBadHabits_EmptyList_ClearsCollection` | Call with empty → `LinkedBadHabits.Count == 0` |

### A2. Implement Domain Changes

**`src/LifeGrid.Domain/UserProfile/UserEconomy.cs`**
- Add `internal void GrantSurveyBonusShield()`:
  - `MaxShieldCap = 3`
  - `ShieldsAvailable++` (safe: cap is now 3, first call always valid)

**`src/LifeGrid.Domain/UserProfile/UserProfile.cs`**
- Add `public bool IsViceSurveyCompleted { get; private set; }` (default `false`)
- Add `public void GrantSurveyBonusShield()`:
  ```csharp
  if (IsViceSurveyCompleted) return; // idempotent
  Economy.GrantSurveyBonusShield();
  IsViceSurveyCompleted = true;
  ```

**`src/LifeGrid.Domain/Goal/Goal.cs`**
- Add `public void SetLinkedBadHabits(IEnumerable<(string description, int dangerLevel)> items)`:
  ```csharp
  _linkedBadHabits.Clear();
  foreach (var (desc, danger) in items)
      _linkedBadHabits.Add(LinkedBadHabit.Create(desc, danger));
  ```
  Note: verify `LinkedBadHabit` has a `Create(string, int)` factory method; add one if missing.

**Verify:** `dotnet test --filter "LifeGrid.Domain.Tests"` — all domain tests pass.

---

## Phase B — Application Layer

> **Persona:** `SENIOR_NET_DEVELOPER.md` + `TDD_SPECIALIST.md`

### B1. New DTOs

**Directory:** `src/LifeGrid.Application/Vice/`

```
SurveyQuestionDto.cs    — record(string Id, string Type, string QuestionText, IReadOnlyList<string>? Options)
SurveyAnswerDto.cs      — record(string QuestionId, string AnswerText)
DetectedViceDto.cs      — record(string Description, int DangerLevel, string GoalDescription)
```

### B2. New Interface

**`src/LifeGrid.Application/Vice/IGeminiViceSurveyService.cs`**
```csharp
public interface IGeminiViceSurveyService
{
    Task<Result<IReadOnlyList<SurveyQuestionDto>>> GenerateQuestionsAsync(
        string goalsContextJson, CancellationToken ct = default);

    Task<Result<IReadOnlyList<DetectedViceDto>>> AnalyzeAnswersAsync(
        string answersJson, string goalsJson, CancellationToken ct = default);
}
```

### B3. Write Application Tests First

**`tests/LifeGrid.Application.Tests/Vice/LaunchViceSurveyCommandTests.cs`**

| Test | Assertion |
|---|---|
| `AlreadyCompleted_ReturnsFailure` | Profile with `IsViceSurveyCompleted=true` → `Result.IsSuccess==false` |
| `NotCompleted_ReturnsSuccess` | Profile with `IsViceSurveyCompleted=false` → `Result.IsSuccess==true` |
| `NoProfile_ReturnsSuccess` | Null profile → `Result.IsSuccess==true` |

**`tests/LifeGrid.Application.Tests/Vice/GetViceSurveyQuestionsQueryTests.cs`**

| Test | Assertion |
|---|---|
| `CallsGeminiWithAllGoalDescriptions` | `GenerateQuestionsAsync` called once; argument contains all goal descriptions |
| `GeminiFailure_PropagatesFailure` | `Result.IsSuccess==false` |

**`tests/LifeGrid.Application.Tests/Vice/SubmitViceSurveyCommandTests.cs`**

| Test | Assertion |
|---|---|
| `HappyPath_SetsLinkedBadHabitsOnMatchingGoals` | Each goal's `SetLinkedBadHabits` called with vices matched by description |
| `HappyPath_GrantsBonusShieldAndMarksComplete` | `profile.IsViceSurveyCompleted==true`; `Economy.MaxShieldCap==3` |
| `HappyPath_CommitsOnce` | `IUnitOfWork.CommitAsync` called exactly once |
| `HappyPath_ReturnsDetectedVices` | `Result.Value` non-empty |
| `GeminiFailure_NoCommit` | `IUnitOfWork.CommitAsync` never called |
| `AlreadyCompleted_ReturnsFailure_NoGeminiCall` | `Result.IsSuccess==false`; `AnalyzeAnswersAsync` never called |

### B4. Implement Commands

**`src/LifeGrid.Application/Vice/LaunchViceSurveyCommand.cs`** + handler
- `record LaunchViceSurveyCommand : IRequest<Result>`
- Handler: `IUserProfileRepository`. Returns `Success()` when profile is null or not completed; `Failure("already_completed")` when completed.

**`src/LifeGrid.Application/Vice/GetViceSurveyQuestionsQuery.cs`** + handler
- `record GetViceSurveyQuestionsQuery : IRequest<Result<IReadOnlyList<SurveyQuestionDto>>>`
- Handler dependencies: `IUserProfileRepository`, `IGoalRepository`, `IGeminiViceSurveyService`
- Serialize all goals to JSON: `[{"description": "...", "ambientTag": "...", "deadlineDate": "..."}]`
- Calls `GenerateQuestionsAsync(goalsContextJson)`

**`src/LifeGrid.Application/Vice/SubmitViceSurveyCommand.cs`** + handler
- `record SubmitViceSurveyCommand(IReadOnlyList<SurveyAnswerDto> Answers) : IRequest<Result<IReadOnlyList<DetectedViceDto>>>`
- Handler dependencies: `IUserProfileRepository`, `IGoalRepository`, `IGeminiViceSurveyService`, `IUnitOfWork`
- Goal matching: group `DetectedViceDto` items by `GoalDescription`; find matching `Goal` aggregate by case-insensitive `Description` comparison; call `goal.SetLinkedBadHabits(...)`. Goals with no matching vices are not modified.
- Commit only after all mutations succeed.

**Verify:** `dotnet test --filter "LifeGrid.Application.Tests.Vice"` — all 11 application tests pass.

---

## Phase C — Infrastructure Layer

> **Persona:** `AI_INTEGRATION_ENGINEER.md` + `SENIOR_NET_DEVELOPER.md`

### C1. Copy Prompt Files

Copy (do not move — keep originals in `docs/specs/assets/prompts/`):
- `docs/specs/assets/prompts/prompt3.1.txt` → `src/LifeGrid.Infrastructure/AI/Prompts/prompt3.1.txt`
- `docs/specs/assets/prompts/prompt3.2.txt` → `src/LifeGrid.Infrastructure/AI/Prompts/prompt3.2.txt`

### C2. Register as EmbeddedResource

**`src/LifeGrid.Infrastructure/LifeGrid.Infrastructure.csproj`** — add to existing `<ItemGroup>`:
```xml
<EmbeddedResource Include="AI\Prompts\prompt3.1.txt" />
<EmbeddedResource Include="AI\Prompts\prompt3.2.txt" />
```

### C3. Write Infrastructure Tests First

**`tests/LifeGrid.Infrastructure.Tests/AI/GeminiViceSurveyServiceTests.cs`**

| Test | Assertion |
|---|---|
| `GenerateQuestions_AllGoalsInPrompt` | Mock `IChatClient` captures the prompt string; assert all goal descriptions are present |
| `GenerateQuestions_ValidJson_ReturnsQuestions` | Correct count of `SurveyQuestionDto` returned |
| `GenerateQuestions_MalformedJson_ReturnsFailure` | `Result.IsSuccess==false` without throwing |
| `AnalyzeAnswers_PlaceholdersSubstituted` | Prompt does not contain literal `${USER_SURVEY_ANSWERS_JSON}` |
| `AnalyzeAnswers_ValidJson_ReturnsFlatViceList` | `DetectedViceDto` list with `GoalDescription` populated |
| `AnalyzeAnswers_MalformedJson_ReturnsFailure` | `Result.IsSuccess==false` without throwing |
| `RateLimit_ReturnsFailure` | `HttpRequestException(429)` → `Result.IsSuccess==false` |

### C4. Implement GeminiViceSurveyService

**`src/LifeGrid.Infrastructure/AI/GeminiViceSurveyService.cs`**
```
internal sealed class GeminiViceSurveyService(IChatClient chatClient)
    : IGeminiViceSurveyService
```

- Static fields: `Prompt31Template = LoadEmbeddedPrompt("prompt3.1.txt")`, `Prompt32Template = LoadEmbeddedPrompt("prompt3.2.txt")`.
- `LoadEmbeddedPrompt` — reuse same pattern as `GeminiHabitGenerationService` (resource name `LifeGrid.Infrastructure.AI.Prompts.{fileName}`).
- `StripCodeFences` — reuse same helper (consider extracting to a shared `GeminiParsingHelpers` static class, or duplicate the 15-line method).
- `GenerateQuestionsAsync`:
  - Prompt = `$"[Current date: {today}]\n\n{Prompt31Template}\n\nUser's Active Goals:\n{goalsContextJson}"`
  - Parses response root object; extracts `questions` JSON array.
  - Maps each element: `id`, `type`, `question_text`, `options` (nullable array).
- `AnalyzeAnswersAsync`:
  - Prompt = `Prompt32Template.Replace("${USER_SURVEY_ANSWERS_JSON}", answersJson).Replace("${USER_GOALS_JSON}", goalsJson)`
  - Parses response root array; for each goal element extracts `data.description` + `bad_habits` array.
  - Flattens into `DetectedViceDto` list (carries `GoalDescription` from parent element).
- Error handling: `HttpRequestException(429)` → `Result.Failure(ex.Message)`; `JsonException` → `Result.Failure(...)`.

### C5. UserProfileConfiguration Update

**`src/LifeGrid.Infrastructure/Data/EntityConfigurations/UserProfileConfiguration.cs`**
- Add inside `Configure`: `builder.Property(e => e.IsViceSurveyCompleted);`

### C6. EF Core Migration

```powershell
dotnet ef migrations add Phase9_AddViceSurveyCompleted --project src/LifeGrid.Infrastructure
```

Verify generated migration adds one column to `UserProfiles` with default `false`.

### C7. DI Registration

**`src/LifeGrid.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs`**
- Add: `services.AddTransient<IGeminiViceSurveyService, GeminiViceSurveyService>();`

**Verify:** `dotnet test --filter "LifeGrid.Infrastructure.Tests"` — all infrastructure tests pass.

---

## Phase D — Presentation Layer

> **Persona:** `MAUI_UX_ENGINEER.md`  
> **Constraint:** Design tokens from `Colors.xaml`; 2px corner radius; DM Mono for headlines, Share Tech Mono for body/labels.

### D1. ViceSurveyViewModel

**`src/LifeGrid.Presentation/ViewModels/ViceSurveyViewModel.cs`**

```csharp
internal enum ViceSurveyState { Loading, Questions, Analyzing, Complete }

public partial class ViceSurveyViewModel(IMediator mediator) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadingState), nameof(IsQuestionsState),
                              nameof(IsAnalyzingState), nameof(IsCompleteState))]
    private ViceSurveyState _state = ViceSurveyState.Loading;

    public bool IsLoadingState   => State == ViceSurveyState.Loading;
    public bool IsQuestionsState => State == ViceSurveyState.Questions;
    public bool IsAnalyzingState => State == ViceSurveyState.Analyzing;
    public bool IsCompleteState  => State == ViceSurveyState.Complete;

    [ObservableProperty] private string  _progressText       = string.Empty;
    [ObservableProperty] private double  _progress           = 0.0;
    [ObservableProperty] private string  _questionText       = string.Empty;
    [ObservableProperty] private bool    _isMultipleChoice;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpenEnded))]
    private bool    _isMultipleChoiceInner;            // drives IsOpenEnded = !IsMultipleChoice
    [ObservableProperty] private IReadOnlyList<string>         _options         = [];
    [ObservableProperty] private string?                       _selectedOption;
    [ObservableProperty] private string                        _freeTextAnswer  = string.Empty;
    [ObservableProperty] private bool                          _isFinalQuestion;
    [ObservableProperty] private string                        _actionButtonLabel = "Next";
    [ObservableProperty] private IReadOnlyList<DetectedViceDto> _detectedVices   = [];

    public bool IsOpenEnded => !IsMultipleChoice;

    private IReadOnlyList<SurveyQuestionDto> _questions = [];
    private int                              _currentIndex;
    private readonly List<SurveyAnswerDto>   _collectedAnswers = new();

    public async Task LoadAsync() { ... }
    private void ShowQuestion(int index) { ... }

    [RelayCommand] private void SelectOption(string option) => SelectedOption = option;
    [RelayCommand] private async Task NextAsync() { ... }
    [RelayCommand] private async Task AcceptAndReturnAsync()
        => await Shell.Current.GoToAsync("..");
}
```

*(Exact method bodies follow from the logic described in P9.6 requirements.)*

**Note:** Add `[NotifyPropertyChangedFor(nameof(IsOpenEnded))]` on `_isMultipleChoice` so XAML `IsVisible` bindings update when question type changes.

### D2. ViceSurveyPage.xaml

**`src/LifeGrid.Presentation/Pages/ViceSurveyPage.xaml`**

Page-level structure (follow existing page pattern — `Grid` with `RowDefinitions="*,Auto"`, inner scroll/content in row 0, `AdBannerView` in row 1):

```xml
<ContentPage ... Title="Hidden Vices Survey" BackgroundColor="{StaticResource Background}">
  <Grid RowDefinitions="*,Auto">
    <Grid Grid.Row="0">                          <!-- state container -->

      <!-- Loading Panel -->
      <VerticalStackLayout IsVisible="{Binding IsLoadingState}" ...>
        <ActivityIndicator IsRunning="True" Color="{StaticResource Primary}" />
        <Label Text="Generating your personalized survey..."
               FontFamily="ShareTechMono" TextColor="{StaticResource OnSurface}" ... />
      </VerticalStackLayout>

      <!-- Questions Panel -->
      <Grid IsVisible="{Binding IsQuestionsState}" RowDefinitions="Auto,*,Auto" ...>
        <!-- Row 0: Progress -->
        <VerticalStackLayout Grid.Row="0" ...>
          <Label Text="{Binding ProgressText}" FontFamily="ShareTechMono" ... />
          <ProgressBar Progress="{Binding Progress}" ProgressColor="{StaticResource Primary}" />
        </VerticalStackLayout>

        <!-- Row 1: Question + Answers -->
        <ScrollView Grid.Row="1">
          <VerticalStackLayout Padding="{StaticResource ScreenMargin}" ...>
            <Label Text="{Binding QuestionText}" Style="{StaticResource HeadlineStyle}" />

            <!-- Multiple-choice options -->
            <VerticalStackLayout IsVisible="{Binding IsMultipleChoice}" ...>
              <BindableLayout.ItemsSource>
                <Binding Path="Options" />
              </BindableLayout.ItemsSource>
              <BindableLayout.ItemTemplate>
                <DataTemplate x:DataType="x:String">
                  <Border StrokeShape="RoundRectangle 2" ...>
                    <Border.GestureRecognizers>
                      <TapGestureRecognizer
                        Command="{Binding Source={RelativeSource AncestorType={x:Type vm:ViceSurveyViewModel}},
                                          Path=SelectOptionCommand}"
                        CommandParameter="{Binding .}" />
                    </Border.GestureRecognizers>
                    <Label Text="{Binding .}" FontFamily="ShareTechMono" ... />
                  </Border>
                </DataTemplate>
              </BindableLayout.ItemTemplate>
            </VerticalStackLayout>

            <!-- Open-ended input -->
            <Editor IsVisible="{Binding IsOpenEnded}"
                    Text="{Binding FreeTextAnswer}"
                    MinimumHeightRequest="120"
                    FontFamily="ShareTechMono"
                    TextColor="{StaticResource OnSurface}" />
          </VerticalStackLayout>
        </ScrollView>

        <!-- Row 2: Action Button -->
        <Button Grid.Row="2"
                Text="{Binding ActionButtonLabel}"
                Command="{Binding NextCommand}"
                BackgroundColor="{StaticResource Primary}"
                TextColor="{StaticResource OnPrimary}"
                CornerRadius="2" Margin="{StaticResource ScreenMargin}" />
      </Grid>

      <!-- Analyzing Panel -->
      <VerticalStackLayout IsVisible="{Binding IsAnalyzingState}" ...>
        <ActivityIndicator IsRunning="True" Color="{StaticResource Primary}" />
        <Label Text="Analyzing your behavioral patterns..."
               FontFamily="ShareTechMono" TextColor="{StaticResource OnSurface}" ... />
      </VerticalStackLayout>

      <!-- Complete Panel -->
      <Grid IsVisible="{Binding IsCompleteState}" RowDefinitions="Auto,*,Auto">
        <Label Grid.Row="0" Text="Hidden Vices Identified"
               Style="{StaticResource HeadlineStyle}"
               TextColor="{StaticResource Primary}" />
        <CollectionView Grid.Row="1" ItemsSource="{Binding DetectedVices}">
          <CollectionView.ItemTemplate>
            <DataTemplate x:DataType="vice:DetectedViceDto">
              <VerticalStackLayout ...>
                <Label Text="{Binding Description}" FontFamily="ShareTechMono" ... />
                <Label FontFamily="ShareTechMono">
                  <!-- Danger level colored via DataTrigger (Error for ≥4, Primary for ≤3) -->
                </Label>
              </VerticalStackLayout>
            </DataTemplate>
          </CollectionView.ItemTemplate>
        </CollectionView>
        <Button Grid.Row="2" Text="Accept &amp; Return"
                Command="{Binding AcceptAndReturnCommand}"
                BackgroundColor="{StaticResource Primary}"
                TextColor="{StaticResource OnPrimary}"
                CornerRadius="2" ... />
      </Grid>

    </Grid>
    <controls:AdBannerView Grid.Row="1" />
  </Grid>
</ContentPage>
```

**`src/LifeGrid.Presentation/Pages/ViceSurveyPage.xaml.cs`**
```csharp
public partial class ViceSurveyPage : ContentPage
{
    private readonly ViceSurveyViewModel _vm;
    public ViceSurveyPage(ViceSurveyViewModel vm)
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

### D3. Navigation Wiring

**`src/LifeGrid.Presentation/AppShell.xaml.cs`**
- Add to constructor: `Routing.RegisterRoute("vice-survey", typeof(ViceSurveyPage));`

### D4. Update UserSetupViewModel

**`src/LifeGrid.Presentation/ViewModels/UserSetupViewModel.cs`**
- Replace `private void DetectHiddenVices() { }` with:
  ```csharp
  [RelayCommand]
  private async Task DetectHiddenVicesAsync()
  {
      var result = await mediator.Send(new LaunchViceSurveyCommand());
      if (result.IsSuccess)
          await Shell.Current.GoToAsync("vice-survey");
      else
          await Application.Current!.MainPage!.DisplayAlert(
              string.Empty,
              "Hidden Vices already established. Factory Reset required to retake.",
              "OK");
  }
  ```

### D5. DI Registration

**`src/LifeGrid.Presentation/MauiProgram.cs`**
- Add:
  ```csharp
  builder.Services.AddTransient<ViceSurveyViewModel>();
  builder.Services.AddTransient<ViceSurveyPage>();
  ```

---

## Phase E — Final Verification

```
dotnet build LifeGrid.slnx
dotnet test
```

Expected: 0 errors, 0 warnings, ~181 tests passing.

**Manual smoke-test checklist:**
- [ ] Cold start → go to User Setup Hub → tap "Detect Hidden Vices" → loading spinner appears
- [ ] Survey questions load (multiple-choice and open-ended types)
- [ ] Progress bar advances correctly; reaches 100% on final question
- [ ] Final question button reads "Analyze Profile"
- [ ] After survey submission: completion panel shows vices
- [ ] "Accept & Return" navigates back to User Setup Hub
- [ ] Second tap on "Detect Hidden Vices" → `DisplayAlert` "already established" message

---

## Acceptance Criteria (as delivered)

- `dotnet build LifeGrid.slnx` → 0 errors, 0 warnings. ✓
- `dotnet test` → all 193 tests pass (71 Domain + 64 Application + 58 Infrastructure). ✓
- `UserProfile.IsViceSurveyCompleted` persisted to DB after survey completion. ✓
- `Goal.LinkedBadHabits` rows populated in `GoalLinkedBadHabits` table for each goal mentioned in AI response. ✓
- `UserEconomy.MaxShieldCap == 3` and `ShieldsAvailable` incremented after survey. ✓
- HUD shield counts refresh immediately on the Complete screen (no app restart needed). ✓
- "Detect Hidden Vices" button hidden permanently once survey is completed (no alert path). ✓
- No new Phase 9 code touches `LifeGrid.Domain` from outside (Zero-Dependency Rule preserved). ✓
- Gemini API key not logged or exposed in any new code path. ✓

---

## Post-Implementation Corrections (2026-06-19)

Two corrections applied after initial implementation:

### 1. Button visibility instead of alert
**Original plan:** `DetectHiddenVicesAsync` would show a `DisplayAlert` if survey already completed.  
**Correction:** The button is hidden via `IsVisible="{Binding IsViceSurveyAvailable}"`. `UserSetupViewModel.LoadAsync()` sends `LaunchViceSurveyCommand` on every `OnAppearing` to set this flag. Once completed, the button disappears from the UI permanently — no alert, no disabled state.

### 2. Immediate HUD refresh after survey
**Original plan:** No HUD refresh step; user would see stale shield counts until app restart.  
**Correction:** `ViceSurveyViewModel` constructor now accepts `HudViewModel hud` (singleton). Immediately after `State = Complete` is set (inside `NextAsync`), `await hud.LoadAsync()` is called. Shield count and cap update on the completion screen itself, before the user taps "Accept & Return".

---

## Implementation Notes (resolved)

- **`LinkedBadHabit.Create(string, int)`** — factory method was missing; added in Phase A.
- **Multiple-choice selection pattern** — implemented as `ObservableCollection<OptionItemViewModel>` with `Action<OptionItemViewModel>` callback, not `BindableLayout` with `SelectOptionCommand`. Each `OptionItemViewModel` manages its own `IsSelected` state and calls back to the parent VM's `OnOptionSelected` method.
- **Danger level coloring** — implemented as `DangerLevelToColorConverter` registered in `App.xaml`; `DataTrigger` approach was not used.
- **`ViceSurveyState` enum** must be `public` (not `internal`) — CommunityToolkit.Mvvm source generator emits a `public State` property, causing CS0053 if the type is `internal`.
- **`Border.CornerRadius`** does not exist in MAUI — use `StrokeShape="RoundRectangle 2"` instead. `Border.Stroke` requires a `Brush` resource (e.g., `{StaticResource SecondaryBrush}`), not a `Color`.
- **EF migration startup project** — `dotnet ef migrations add Phase9_AddViceSurveyCompleted --project src/LifeGrid.Infrastructure` (no `--startup-project` flag).
