# Phase 13 — Setup Flow Hardening: Picker-Based Start Date & Retry UX
**Status:** DONE — completed 2026-06-22

## Source Documents
- Requirements: `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` §P13
- Supersedes: P14.2 (DatePicker → Picker), adds P13.3 (HttpClient), P13.4 (Retry UX)

---

## Summary

Follow-on hardening pass applied after Phase 14's initial implementation, addressing three issues discovered during device testing and build verification.

---

## Corrections Applied

### C1 — Monday-Only Picker (P13.2)

**Problem:** MAUI `DatePicker` cannot grey out non-Monday days on Android; the snap handler accepted `DateTime?` in .NET 10 but the original code treated it as `DateTime`.

**Fix:** Replaced `DatePicker` + snap handler with a `Picker` pre-populated with 52 upcoming Mondays (`_anchorMonday` static field → `AvailableMondayLabels`). `SelectedMondayIndex` ↔ `ChosenStartDate` synced via `OnSelectedMondayIndexChanged` partial method.

**Files changed:**
- `src/LifeGrid.Presentation/ViewModels/SetupViewModel.cs` — removed `EarliestSelectableDate`; added `_anchorMonday`, `_availableMondays`, `AvailableMondayLabels`, `_selectedMondayIndex`, `OnSelectedMondayIndexChanged`
- `src/LifeGrid.Presentation/Pages/SetupPage.xaml` — `DatePicker` → `Picker` bound to `AvailableMondayLabels` / `SelectedMondayIndex`
- `src/LifeGrid.Presentation/Pages/SetupPage.xaml.cs` — `OnStartDatePickerDateSelected` handler removed; `GoalAggregate` using alias removed

---

### C2 — HttpClient Timeout & Keep-Alive (P13.3)

**Problem:** Default 100-second `HttpClient` timeout caused "Socket closed" errors on the second sequential Gemini call (prompt2.2) when responses exceeded 100 seconds.

**Fix:** Replaced bare `services.AddSingleton<HttpClient>()` with a configured factory using `SocketsHttpHandler` (keep-alive pings every 20 s, 15-s timeout) and a 5-minute `Timeout`.

**Files changed:**
- `src/LifeGrid.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs`

---

### C3 — Habit Generation Retry UX (P13.4)

**Problem:** `ConfirmAndInitializeAsync` set `IsRefinementActive = false` before calling `AutoResumeHabitGenerationAsync`. When habit generation failed, the user was left on STATE A (goal entry form) with no retry path. Re-tapping "Confirm & Initialize" would fail because `FinalizeGoalCommand` guards against a session already in `Step1_ExecutionVerified`.

**Fix:**
1. Added `private bool _goalAlreadyFinalized` — `FinalizeGoalCommand` is only dispatched on first call; subsequent calls skip to habit generation retry.
2. Moved `IsRefinementActive = false` into `AutoResumeHabitGenerationAsync`, set only on success — failure leaves user in STATE D with error + retry button.
3. `ValidationError` cleared at top of `ConfirmAndInitializeAsync`.

**Files changed:**
- `src/LifeGrid.Presentation/ViewModels/SetupViewModel.cs`

---

### C4 — Additional Goal.Create() Call Sites (P13 build discovery)

During `dotnet build` verification, 9 additional test files were found with the old 6-parameter `Goal.Create()` signature. All updated to the Phase 14 7-parameter signature (`startDate` inserted before `creationDate`).

**Files changed:**
- `tests/LifeGrid.Domain.Tests/Goal/GoalRefinementAnswerTests.cs`
- `tests/LifeGrid.Domain.Tests/Vice/GoalLinkedBadHabitsTests.cs`
- `tests/LifeGrid.Application.Tests/Vice/GetViceSurveyQuestionsQueryTests.cs`
- `tests/LifeGrid.Application.Tests/Vice/SubmitViceSurveyCommandTests.cs`
- `tests/LifeGrid.Infrastructure.Tests/Data/WeekRepositoryTests.cs`
- `tests/LifeGrid.Infrastructure.Tests/Data/HabitRepositoryTests.cs`
- `tests/LifeGrid.Infrastructure.Tests/Data/FactoryResetServiceTests.cs`
- `tests/LifeGrid.Infrastructure.Tests/Data/WeekDeduplicationTests.cs`
- `tests/LifeGrid.Infrastructure.Tests/Schema/GoalRefinementAnswerSchemaTests.cs`

---

## Acceptance Results

| Check | Result |
|---|---|
| `dotnet build` | **0 Error(s)** |
| `dotnet test` (Domain) | **93 passed** |
| `dotnet test` (Application) | **96 passed** |
| `dotnet test` (Infrastructure) | **64 passed** |
| **Total** | **253 passed** |
