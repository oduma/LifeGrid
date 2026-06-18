# Phase 5 Implementation Plan: AI Validation & Goal Refinement Pipeline

**Status:** ✅ COMPLETED  
**Requirements Source:** P5.1–P5.15 (`docs/requirements/FUNCTIONAL_REQUIREMENTS.md`)  
**Spec References:** `functional-requirements.md §1.1–1.4`, `prompt1.txt`, `prompt2.txt`, `data-structure.json`

---

## Clarification Decisions

| Question | Answer |
|---|---|
| Validation retry flow | Inline error — `retry_prompt` shown above existing text entry; no new visual state |
| Session cleanup after Confirm & Initialize | Keep & advance — `CurrentStep → Step1_ExecutionVerified`; row is NOT deleted |
| AI response storage | Staged in `OnboardingSession` columns (`ValidatedGoalJson`, `RefinementQuestionsJson`); final committed home is the `Goal` aggregate and its `GoalRefinementAnswers` owned collection |
| Refinement Q&A schema | Owned collection on `Goal` — separate `GoalRefinementAnswers` SQL table, FK to Goal |

---

## Execution Phases

### Phase A — Data Structure & Schema Design

**A.1 data-structure.json** ✓ *(already updated)*
- Added `Goal_Refinement_Answers` array to `Goal` with `RefinementAnswerId`, `RankOrder`, `Question`, `Answer?`.
- Updated `OnboardingProgressCache.Current_Step` enum values to include the three new steps.
- Added `Validated_Goal_Json?` and `Refinement_Questions_Json?` staging columns to `OnboardingProgressCache`.

---

### Phase B — Domain Layer

**B.1 Expand `OnboardingStep` enum**  
File: `src/LifeGrid.Domain/Onboarding/OnboardingStep.cs`  
Add after `Step1_GoalDraftCaptured`:
```
Step1_AwaitingValidation,
Step1_RefinementQuestionsActive,
Step1_ExecutionVerified
```

**B.2 Expand `OnboardingSession`**  
File: `src/LifeGrid.Domain/Onboarding/OnboardingSession.cs`

New properties:
```csharp
public string? ValidatedGoalJson     { get; private set; }
public string? RefinementQuestionsJson { get; private set; }
```

New methods:
```csharp
public void AdvanceToAwaitingValidation()
public void AdvanceToRefinementQuestionsActive(string validatedGoalJson, string refinementQuestionsJson)
public void AdvanceToExecutionVerified()          // also sets both JSON fields to null
public void RevertToGoalDraftCaptured()           // used when validation returns Invalid
```

**B.3 New `GoalRefinementAnswer` entity**  
File: `src/LifeGrid.Domain/Goal/GoalRefinementAnswer.cs`
```csharp
public sealed class GoalRefinementAnswer
{
    private GoalRefinementAnswer() { }
    internal static GoalRefinementAnswer Create(int rankOrder, string question, string? answer) => new()
    {
        RefinementAnswerId = Guid.NewGuid(),
        RankOrder = rankOrder,
        Question  = question,
        Answer    = answer
    };
    public Guid    RefinementAnswerId { get; private set; }
    public int     RankOrder          { get; private set; }
    public string  Question           { get; private set; } = null!;
    public string? Answer             { get; private set; }
}
```

**B.4 Expand `Goal` aggregate**  
File: `src/LifeGrid.Domain/Goal/Goal.cs`

Add:
```csharp
private readonly List<GoalRefinementAnswer> _refinementAnswers = new();
public IReadOnlyCollection<GoalRefinementAnswer> RefinementAnswers => _refinementAnswers.AsReadOnly();

public void SetRefinementAnswers(IEnumerable<(int rankOrder, string question, string? answer)> items)
{
    _refinementAnswers.Clear();
    foreach (var (r, q, a) in items)
        _refinementAnswers.Add(GoalRefinementAnswer.Create(r, q, a));
}
```

---

### Phase C — Application Layer: Interfaces & DTOs

All new files in `src/LifeGrid.Application/Goal/` and `src/LifeGrid.Application/Onboarding/`:

**C.1 DTOs**  
`src/LifeGrid.Application/Goal/ValidatedGoalDto.cs`
```csharp
public sealed record ValidatedGoalDto(
    string   Description,
    string   Duration,
    DateTime DeadlineDate,
    string   AmbientTag);
```

`src/LifeGrid.Application/Goal/RefinementQuestionDto.cs`
```csharp
public sealed record RefinementQuestionDto(int RankOrder, string Question);
```

`src/LifeGrid.Application/Goal/GeminiValidationResult.cs`
```csharp
public abstract record GeminiValidationResult
{
    public sealed record Valid(ValidatedGoalDto Data)   : GeminiValidationResult;
    public sealed record Invalid(string RetryPrompt)    : GeminiValidationResult;
}
```

**C.2 `IGeminiGoalValidationService`**  
`src/LifeGrid.Application/Goal/IGeminiGoalValidationService.cs`
```csharp
public interface IGeminiGoalValidationService
{
    Task<Result<GeminiValidationResult>> ValidateGoalAsync(
        string rawDraft, CancellationToken ct = default);

    Task<Result<IReadOnlyList<RefinementQuestionDto>>> GenerateRefinementQuestionsAsync(
        string validatedGoalJson, CancellationToken ct = default);
}
```

**C.3 `IGoalRepository`**  
`src/LifeGrid.Application/Goal/IGoalRepository.cs`
```csharp
public interface IGoalRepository
{
    Task AddAsync(Goal goal, CancellationToken ct = default);
}
```

---

### Phase D — Application Layer: Commands

**D.1 `TriggerGoalValidationCommand`**  
`src/LifeGrid.Application/Onboarding/Commands/TriggerGoalValidationCommand.cs`

- `IRequest<Result<IReadOnlyList<RefinementQuestionDto>>>`
- Handler steps (see P5.7 requirements):
  1. Guard: session must exist and have a non-empty `RawGoalDraft`.
  2. `session.AdvanceToAwaitingValidation()` → `UpsertAsync`.
  3. Call `ValidateGoalAsync(draft)`.
  4. On `Invalid`: `session.RevertToGoalDraftCaptured()` → `UpsertAsync` → `Result.Failure(retryPrompt)`.
  5. On `Valid`: call `GenerateRefinementQuestionsAsync(validatedGoalJson)`.
  6. On questions success: `session.AdvanceToRefinementQuestionsActive(validatedJson, questionsJson)` → `UpsertAsync`.
  7. Return `Result.Success(questions)`.

**D.2 `FinalizeGoalCommand`**  
`src/LifeGrid.Application/Onboarding/Commands/FinalizeGoalCommand.cs`

- `IRequest<Result>`; carries `IReadOnlyList<(int RankOrder, string Answer)> UserAnswers`
- Handler steps (see P5.8 requirements):
  1. Guard: session must be in `Step1_RefinementQuestionsActive`.
  2. Get `UserProfile` — fail if null.
  3. Deserialize `ValidatedGoalJson` → `ValidatedGoalDto`.
  4. Deserialize `RefinementQuestionsJson` → `List<RefinementQuestionDto>`.
  5. `Goal.Create(...)` → `goal.SetRefinementAnswers(merged Q+A)`.
  6. `IGoalRepository.AddAsync(goal)`.
  7. `session.AdvanceToExecutionVerified()` → `UpsertAsync`.
  8. Return `Result.Success()`.

---

### Phase E — Infrastructure Layer

**E.1 Prompt files as EmbeddedResource**  
Copy `docs/specs/assets/prompts/prompt1.txt` and `prompt2.txt` into  
`src/LifeGrid.Infrastructure/AI/Prompts/prompt1.txt` and `prompt2.txt`.  
Mark both as `<EmbeddedResource>` in `LifeGrid.Infrastructure.csproj`.

**E.2 `GeminiHttpChatClient`**  
`src/LifeGrid.Infrastructure/AI/GeminiHttpChatClient.cs`

```csharp
internal sealed class GeminiHttpChatClient(HttpClient http, string apiKey) : IChatClient
{
    // POST https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-lite:generateContent?key={apiKey}
    // Body: { "contents": [{ "parts": [{ "text": "<prompt>" }] }] }
    // Extracts: candidates[0].content.parts[0].text
    public async Task<ChatCompletion> GetResponseAsync(
        IList<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken ct = default) { ... }

    // All other IChatClient members → throw NotSupportedException
}
```

**E.3 `GeminiGoalValidationService`**  
`src/LifeGrid.Infrastructure/AI/GeminiGoalValidationService.cs`

- Constructor: `IChatClient chatClient`
- `LoadPrompt(string resourceName)` — reads EmbeddedResource once, caches in `static readonly string`
- `ValidateGoalAsync`:
  - Prepend `[Current date: {DateTime.UtcNow:MMMM d, yyyy}]` to prompt1 content
  - Replace `${USER_INPUT_TEXT}` with `rawDraft`
  - Call `IChatClient.GetResponseAsync`
  - Parse JSON with `System.Text.Json`; handle both `{ "isValid": true, "goal": {...} }` and `{ "isValid": false, "retry_prompt": "..." }`
  - Return typed `GeminiValidationResult.Valid` or `.Invalid`; catch `JsonException` → `Result.Failure`
- `GenerateRefinementQuestionsAsync`:
  - Replace `${VALIDATED_GOAL_JSON}` in prompt2 with `validatedGoalJson`
  - Call `IChatClient.GetResponseAsync`
  - Parse JSON array of `{ "RankOrder": N, "Question": "..." }`
  - Return `Result.Success(IReadOnlyList<RefinementQuestionDto>)`; catch `JsonException` → `Result.Failure`

**E.4 `GoalRepository`**  
`src/LifeGrid.Infrastructure/Data/Repositories/GoalRepository.cs`

```csharp
internal sealed class GoalRepository(LifeGridDbContext db) : IGoalRepository
{
    public async Task AddAsync(Goal goal, CancellationToken ct = default)
    {
        db.Goals.Add(goal);
        await db.SaveChangesAsync(ct);
    }
}
```

**E.5 EF Configuration Updates**

`OnboardingSessionConfiguration.cs` — add:
```csharp
builder.Property(e => e.ValidatedGoalJson);
builder.Property(e => e.RefinementQuestionsJson);
```

`GoalConfiguration.cs` — add:
```csharp
builder.OwnsMany(g => g.RefinementAnswers, ra =>
{
    ra.ToTable("GoalRefinementAnswers");
    ra.WithOwner().HasForeignKey("GoalId");
    ra.HasKey(r => r.RefinementAnswerId);
    ra.Property(r => r.Question).HasMaxLength(500);
    ra.Property(r => r.Answer).HasMaxLength(2000);
});
```

**E.6 DI Registration**  
`InfrastructureServiceExtensions.cs` — add:
```csharp
services.AddHttpClient<GeminiHttpChatClient>();
services.AddTransient<IChatClient, GeminiHttpChatClient>();
services.AddTransient<IGeminiGoalValidationService, GeminiGoalValidationService>();
services.AddScoped<IGoalRepository, GoalRepository>();
```

**E.7 EF Migration**  
```
dotnet ef migrations add AddGoalRefinementAnswerSchemaAndSessionStagingFields --project src/LifeGrid.Infrastructure
```
Up() adds:
- `GoalRefinementAnswers` table (RefinementAnswerId PK, GoalId FK, RankOrder, Question, Answer nullable)
- `ValidatedGoalJson TEXT NULL` column on `OnboardingProgressCache`
- `RefinementQuestionsJson TEXT NULL` column on `OnboardingProgressCache`

---

### Phase F — Presentation Layer

**F.1 `SetupViewModel` updates**  
`src/LifeGrid.Presentation/ViewModels/SetupViewModel.cs`

New properties:
```csharp
[ObservableProperty] private bool   _isValidating;
[ObservableProperty] private string _validationErrorMessage = string.Empty;
[ObservableProperty] private bool   _isRefinementActive;
[ObservableProperty] private bool   _isExecutionVerified;
[ObservableProperty] private string _validatedGoalSummary  = string.Empty; // "Goal: X | Deadline: Y | Duration: Z"

public ObservableCollection<RefinementQuestionDto> RefinementQuestions { get; } = new();
public ObservableCollection<string>               RefinementAnswers   { get; } = new();
```

Modified `LoadAsync()`: handle `Step1_RefinementQuestionsActive` resume — deserialize session's `RefinementQuestionsJson`, populate `RefinementQuestions` + blank `RefinementAnswers`, set `IsRefinementActive = true`.

Modified `CompleteStep1Async()`:
```csharp
IsValidating = true;
ValidationErrorMessage = string.Empty;
var result = await mediator.Send(new TriggerGoalValidationCommand());
IsValidating = false;
if (!result.IsSuccess) { ValidationErrorMessage = result.Error!; return; }
// populate RefinementQuestions / RefinementAnswers
IsRefinementActive = true;
```

New `ConfirmAndInitializeAsync()`:
```csharp
var answers = RefinementAnswers
    .Select((a, i) => (RefinementQuestions[i].RankOrder, a))
    .ToList();
var result = await mediator.Send(new FinalizeGoalCommand(answers));
if (result.IsSuccess) IsExecutionVerified = true;
```

**F.2 `SetupPage.xaml` updates**  
Replaces the existing two-state toggle with a four-state structure, all controlled by `IsValidating`, `IsRefinementActive`, `IsExecutionVerified`:

```
State A (default — Unstarted/retry):
  [ValidationErrorMessage Label — error color, visible if non-empty]
  [Goal text entry]
  [Next Button → CompleteStep1Command]

State C (IsValidating):
  [Label "Analyzing your goal..." — Share Tech Mono, center-aligned]
  (typewriter flicker via ActivityIndicator + animated label)

State D (IsRefinementActive):
  [Read-only block — DM Mono HeadlineStyle]
    Goal: {ValidatedGoalSummary}
  [For each RefinementQuestion:]
    [Label: question text — Share Tech Mono]
    [Entry: bound to RefinementAnswers[i] — 2px corner radius]
  [Button "Confirm & Initialize" → ConfirmAndInitializeCommand]

State E (IsExecutionVerified):
  [Label "Goal Refined and Stored. Ready for Phase 6 Habit Generation."
   — DM Mono, Primary color #35f8db]
```

Note: `ObservableCollection<string>` bindings for variable-length answer entries require a `BindableLayout` or code-behind approach; the exact binding pattern will be determined during implementation (MAUI limitation with indexed collection binding).

---

### Phase G — Testing

**G.1 Domain tests** (`tests/LifeGrid.Domain.Tests/`)

`Onboarding/OnboardingSessionStep5Tests.cs`:
- `AdvanceToAwaitingValidation_SetsCorrectStep`
- `AdvanceToRefinementQuestionsActive_SetsStepAndStoresJson`
- `AdvanceToExecutionVerified_ClearsStagingFields`
- `RevertToGoalDraftCaptured_ResetsStep`

`Goal/GoalRefinementAnswerTests.cs`:  
*(use `using GoalAggregate = LifeGrid.Domain.Goal.Goal;` alias — per P4.13 correction)*
- `SetRefinementAnswers_CreatesOwnedCollection`
- `SetRefinementAnswers_ReplacesExistingAnswers`
- `SetRefinementAnswers_WithNullAnswer_IsAllowed`

**G.2 Application tests** (`tests/LifeGrid.Application.Tests/`)

`Onboarding/TriggerGoalValidationCommandTests.cs`:
- `Handle_NoSession_ReturnsFailure`
- `Handle_EmptyDraft_ReturnsFailure`
- `Handle_GeminiReturnsInvalid_RevertsSessionAndReturnsFailureWithRetryPrompt`
- `Handle_GeminiReturnsValid_AdvancesSessionToRefinementActiveAndReturnsQuestions`
- `Handle_GeminiRefinementFails_ReturnsFailure`

`Onboarding/FinalizeGoalCommandTests.cs`:
- `Handle_SessionNotInRefinementActive_ReturnsFailure`
- `Handle_NoUserProfile_ReturnsFailure`
- `Handle_ValidFlow_CreatesGoalWithRefinementAnswersAndAdvancesSession`
- `Handle_ValidFlow_ClearsStagingJsonOnSession`
- `Handle_ValidFlow_DoesNotWriteGoalBeforeExplicitCall` (asserts repo not called if command not issued)

**G.3 Infrastructure tests** (`tests/LifeGrid.Infrastructure.Tests/`)

`AI/GeminiGoalValidationServiceTests.cs`:
- `ValidateGoal_ValidJson_ReturnsValidResult`
- `ValidateGoal_InvalidJson_ReturnsInvalidWithRetryPrompt`
- `ValidateGoal_MalformedJson_ReturnsFailureWithoutThrowing`
- `GenerateRefinementQuestions_ValidJson_ReturnsQuestions`
- `GenerateRefinementQuestions_MalformedJson_ReturnsFailure`

`Schema/GoalRefinementAnswerSchemaTests.cs`:
- `GoalRefinementAnswers_TableExists_AfterMigration`
- `GoalRefinementAnswer_CanBeWrittenAndReadBack_WithNullAnswer`
- `GoalRefinementAnswer_CanBeWrittenAndReadBack_WithFilledAnswer`
- `OnboardingSession_StagingColumns_AreNullableAndPersist`

---

## Key Risks & Mitigations

| Risk | Mitigation |
|---|---|
| Gemini API returns markdown-wrapped JSON (e.g. `` ```json `` fences) | Strip code fence markers before parsing; covered by infrastructure test |
| `ObservableCollection<string>` indexed binding limitation in MAUI XAML | Use `BindableLayout.ItemsSource` on a `VerticalStackLayout` with an `ItemTemplate`; answers tracked as `ObservableProperty` string list in ViewModel |
| Type alias required for `GoalRefinementAnswer` tests (P4.13 correction) | Test file namespace ends in `Goal` — apply `using GoalRefinementAnswerEntity = ...` alias pattern |
| `IChatClient` not natively mock-friendly from MS.Ext.AI | `IChatClient` is an interface; NSubstitute handles it directly via `Substitute.For<IChatClient>()` |
| Prompt EmbeddedResource names are case-sensitive | Verify manifest name with `assembly.GetManifestResourceNames()` in a unit test assert |

---

## Acceptance Criteria

- `dotnet build` → 0 errors, 0 warnings. ✅
- `dotnet test` → all existing 46 + all new tests pass (87 total). ✅
- All new Application command handlers have 100% branch coverage. ✅
- `GoalRefinementAnswers` and staging columns exist in the SQLite schema after migration. ✅
- No `Goal` row written before "Confirm & Initialize" is triggered. ✅
- Session row remains in DB after Phase 5, with `CurrentStep = Step1_ExecutionVerified`. ✅

---

## Implementation Notes (deviations from plan)

| Area | Planned | Actual |
|---|---|---|
| Gemini model | `gemini-2.0-flash-lite` | `gemini-2.5-pro` (primary), `gemini-2.5-flash` (503 fallback) |
| 503 handling | Not planned | Transparent silent fallback to `gemini-2.5-flash` |
| 429 handling | Not planned | Fail-fast; parse `retryDelay` from Gemini error body; user-readable wait hint |
| Retry loop | Not planned | Removed — fail-fast is better UX on mobile |
| API key guard | Not planned | Empty key detected before HTTP call with setup instruction |
| `RefinementItems` shape | Two parallel collections (`RefinementQuestions` + `RefinementAnswers`) | Single `ObservableCollection<RefinementItem>` where `RefinementItem : ObservableObject` holds question + `[ObservableProperty] _answer` |
| ValidationError property | `ValidationErrorMessage` | `ValidationError` |
| `StringNotEmptyConverter` | Not planned | Added to `LifeGrid.Presentation/Converters/` for XAML binding |
