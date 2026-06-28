# Phase 30 Plan: Weekly Lifecycle Engine & Closure Protocol

**Status:** DONE  
**Requirements source:** `docs/requirements/Phase-30-requirements.md`  
**Finalized requirements:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` (P30.1–P30.10)

---

## Clarifications Recorded (2026-06-25)

| # | Question | Answer |
|---|---|---|
| 1 | Push notification library | **`Plugin.LocalNotification`** (alternatives: Shiny, raw Android NotificationManager — noted below) |
| 2 | Background scheduling | **Android WorkManager** |
| 3 | WeeklyHabitsPage past-week support | Requires refactor — `weekId` query param already wired in VM, but Close/Summary buttons are new |
| 4 | Week.EndDate storage | **Calculated** — `StartDate.AddDays(6)`, never persisted |
| 5 | `Closed` state and gamification | Terminal state — gamification engine ignores it |

### Alternative push notification libraries (for future reference)
- **Shiny** — full background scheduling + local notifications + badge management; heavier dependency tree
- **Raw Android `NotificationManager` + `AlarmManager`** — maximum control, zero third-party dependency, highest boilerplate

---

## Pre-Flight Architecture Notes

- `WeeklyHabitsViewModel` already implements `IQueryAttributable` and accepts `weekId` — no handler refactor needed.
- Route `"week-detail"` is already registered in `AppShell.xaml.cs` for `WeeklyHabitsPage`.
- `WeekStatus` is EF-stored as a string — adding `Closed` is non-breaking; no new EF migration required.
- `WeekSummaryViewModel` reuses `GetWeeklyHabitsQuery` directly (per spec: "read-only replication of the data already presented in WeeklyHabitsView").
- Workers access DI via `IPlatformApplication.Current.Services.CreateScope()` — standard MAUI Android pattern.

---

## Implementation Phases

---

### Phase A: Domain Layer

**A1. Add `Closed` to `WeekStatus` enum**
- File: `src/LifeGrid.Domain/Week/WeekStatus.cs`
- Add `Closed` after `Frozen`. No other changes.

**A2. Add `Close()` to `Week` entity**
- File: `src/LifeGrid.Domain/Week/Week.cs`
- New method:
  ```csharp
  public void Close() => Status = WeekStatus.Closed;
  ```
- `Pause()` remains unchanged (Hibernate/Freeze only).

---

### Phase B: Application Layer

**B1. `CloseWeekCommand` + Handler**
- File: `src/LifeGrid.Application/Week/CloseWeekCommand.cs`
- Command: `record CloseWeekCommand(Guid WeekId) : IRequest<Result>`
- Handler dependencies: `IWeekRepository`, `IUnitOfWork`, `IEconomyStateBroadcaster`
- Logic: fetch week → null check → `week.Close()` → `CommitAsync()` → `broadcaster.Broadcast()`
- Gamification penalty calculations: **stubbed out** (Phase 30 explicit scope boundary)

**B2. `IPushNotificationService` interface**
- File: `src/LifeGrid.Application/Common/IPushNotificationService.cs`
- Interface:
  ```csharp
  public interface IPushNotificationService
  {
      Task SendAsync(string title, string body, string? deepLinkUrl = null,
                     CancellationToken ct = default);
      Task ScheduleAsync(string title, string body, DateTime fireAtLocal,
                         string? deepLinkUrl = null, CancellationToken ct = default);
  }
  ```

**B3. Extend `NotificationRouteParser`**
- File: `src/LifeGrid.Application/Notification/NotificationRouteParser.cs`
- Add two new switch arms:
  - `"week"` → `$"week-detail?weekId={segments[0]}"`
  - `"summary"` → `$"week-summary?weekId={segments[0]}"`

**B4. `IWeekRepository` — new method**
- Interface: `src/LifeGrid.Application/Week/IWeekRepository.cs`
  - Add: `Task<Week?> GetByStartDateAsync(DateTime startDate, CancellationToken ct = default);`
- Implementation: `src/LifeGrid.Infrastructure/Data/Repositories/WeekRepository.cs`
  - `FirstOrDefaultAsync(w => w.StartDate == startDate, ct)`

---

### Phase C: Infrastructure & Platform

**C1. Add NuGet packages to `LifeGrid.Presentation.csproj`**
- `Plugin.LocalNotification` — latest stable for .NET MAUI (v11.x at time of implementation)
- WorkManager binding for Android — `Xamarin.AndroidX.Work.Runtime` (confirm exact version/package name for .NET 10 at implementation time; add under `net10.0-android` condition)

**C2. `LocalPushNotificationService`**
- File: `src/LifeGrid.Presentation/Services/LocalPushNotificationService.cs`
- Implements `IPushNotificationService` using `Plugin.LocalNotification`
- `SendAsync` → `LocalNotificationCenter.Current.ShowAsync(new NotificationRequest { ... })` (no schedule)
- `ScheduleAsync` → same but with `Schedule = new NotificationRequestSchedule { NotifyAt = fireAtLocal }`
- Registered as `Singleton` in `MauiProgram.cs`

**C3. `MondayWeekReminderWorker` (Android WorkManager Worker)**
- File: `src/LifeGrid.Presentation/Platforms/Android/Workers/MondayWeekReminderWorker.cs`
- Extends `AndroidX.Work.Worker`
- `DoWork()`:
  1. `var scope = IPlatformApplication.Current.Services.CreateScope()`
  2. Calculate `previousMonday = today.AddDays(-(int)today.DayOfWeek - 6)` (previous week's Monday)
  3. `weekRepo.GetByStartDateAsync(previousMonday)` — if null or already Closed: return `Result.Success()`
  4. `pushService.SendAsync("Week Ended", "Please review and close your previous week.", $"lifegrid://week/{week.WeekId}")`
  5. `notifRepo.AddAsync(Notification.Create(...))` + `unitOfWork.CommitAsync()`
  6. Return `Result.Success()`

**C4. `WednesdayAutoCloseWorker` (Android WorkManager Worker)**
- File: `src/LifeGrid.Presentation/Platforms/Android/Workers/WednesdayAutoCloseWorker.cs`
- Same DI pattern as Monday worker
- `DoWork()`:
  1. Calculate `previousMonday` (same as above — Wednesday sees the same "previous week")
  2. `weekRepo.GetByStartDateAsync(previousMonday)` — if null or Closed: no-op
  3. `mediator.Send(new CloseWeekCommand(week.WeekId))`
  4. `pushService.SendAsync("Week Auto-Closed", "Your previous week was automatically closed by the system.", $"lifegrid://summary/{week.WeekId}")`
  5. `notifRepo.AddAsync(Notification.Create(...))` + `unitOfWork.CommitAsync()`

**C5. `WeekLifecycleScheduler`**
- File: `src/LifeGrid.Presentation/Platforms/Android/WeekLifecycleScheduler.cs`
- Static method `Schedule()` called once in `MauiProgram.cs` after `builder.Build()`
- Calculates initial delay from `DateTime.Now` to the next Monday 09:00 local
- Enqueues `MondayWeekReminderWorker` as `PeriodicWorkRequest` with 7-day repeat (or `OneTimeWorkRequest` rescheduling itself)
- Same for `WednesdayAutoCloseWorker` targeting next Wednesday 09:00
- Uses `ExistingWorkPolicy.Replace` so re-runs don't duplicate workers

---

### Phase D: Presentation — WeeklyHabitsPage Updates

**D1. `WeeklyHabitsDashboardDto` — confirm fields**
- `StartDate` (DateTime) and `Status` (string) are already present. No changes needed.

**D2. `WeeklyHabitsViewModel` additions**
- Add three `[ObservableProperty]` properties:
  ```csharp
  [ObservableProperty] private bool _isCloseWeekButtonVisible;
  [ObservableProperty] private bool _isSummaryButtonVisible;
  [ObservableProperty] private bool _isLoggingEnabled;
  ```
- In `LoadAsync()`, after fetching `dto`:
  ```csharp
  var isClosed = dto.Status == "Closed";
  var endDate  = dto.StartDate.AddDays(6);
  var isPast   = endDate.Date < DateTime.UtcNow.Date;
  IsLoggingEnabled         = !isClosed;
  IsCloseWeekButtonVisible = isPast && !isClosed;
  IsSummaryButtonVisible   = isClosed;
  ```
- Add `CloseWeekCommand` ([RelayCommand]):
  - Sends `CloseWeekCommand(_weekId)` via MediatR
  - On `result.IsSuccess`: calls `LoadAsync()`
- Add `GoToSummaryCommand` ([RelayCommand]):
  - `Shell.Current.GoToAsync($"week-summary?weekId={_weekId}")`

**D3. `WeeklyHabitsPage.xaml` additions**
- Add "Close the Week" `Button` in Zone A (below week header):
  ```xaml
  <Button Text="CLOSE THE WEEK"
          Command="{Binding CloseWeekCommand}"
          IsVisible="{Binding IsCloseWeekButtonVisible}"
          BackgroundColor="{StaticResource Primary}"
          TextColor="{StaticResource OnPrimary}"
          CornerRadius="2" ... />
  ```
- Add "Go to Week Summary" `Button` similarly, `IsVisible="{Binding IsSummaryButtonVisible}"`
- Disable habit log tap gestures: on the habit card `Border`, add `IsEnabled="{Binding Source={x:Reference PageRoot}, Path=BindingContext.IsLoggingEnabled}"`

---

### Phase E: WeekSummaryPage (New Page)

**E1. `WeekSummaryViewModel`**
- File: `src/LifeGrid.Presentation/ViewModels/WeekSummaryViewModel.cs`
- `IQueryAttributable` — receives `weekId` (Guid)
- `LoadAsync()`: sends `GetWeeklyHabitsQuery(_weekId)`, populates `GoalGroups` (same `WeeklyGoalGroupItem` type but `isFuture=false`, `isCurrentWeek=false`, `isLoggingEnabled=false`), `WeekHeaderText`, `WeekStatusText`
- No commands (no logging, no close, no moment burst)

**E2. `WeekSummaryPage.xaml` + code-behind**
- File: `src/LifeGrid.Presentation/Pages/WeekSummaryPage.xaml`
- Title: "Week Summary"
- Same visual layout as `WeeklyHabitsPage.xaml` — Zone A header + Zone B scrollable goal groups
- All `TapGestureRecognizer` elements removed; all action buttons removed
- Loads in `OnNavigatedTo(NavigatedToEventArgs args)` (same pattern as `NotificationInboxPage`)

**E3. Register route and DI**
- `AppShell.xaml.cs`: `Routing.RegisterRoute("week-summary", typeof(WeekSummaryPage));`
- `MauiProgram.cs`:
  ```csharp
  builder.Services.AddTransient<WeekSummaryViewModel>();
  builder.Services.AddTransient<WeekSummaryPage>();
  builder.Services.AddSingleton<IPushNotificationService, LocalPushNotificationService>();
  ```
- Call `WeekLifecycleScheduler.Schedule()` after `builder.Build()`

---

### Phase F: Tests

**F1. Domain tests** — `src/tests/LifeGrid.Domain.Tests/Week/WeekClosureTests.cs`
- `Close_SetsStatusToClosed`
- `Close_CalledTwice_StatusRemainsClosedWithoutException`

**F2. Application — `CloseWeekCommandTests`** — `tests/LifeGrid.Application.Tests/Week/CloseWeekCommandTests.cs`
- `CloseWeekCommand_Succeeds_CommitsAndBroadcasts`
- `CloseWeekCommand_WeekNotFound_ReturnsFailure`

**F3. Application — Background logic test** — `tests/LifeGrid.Application.Tests/Week/WeekLifecycleServiceTests.cs`
- Extract the "should this week be closed?" decision into a pure static helper or testable service class in Application layer
- `AutoClose_Wednesday_9AM_ClosesUnclosedWeek_AndAddsNotification` — mock clock = Wednesday 09:01; mock `GetByStartDateAsync` returns Active week; verify `CloseWeekCommand` sent + `INotificationRepository.AddAsync` called

**F4. Application — ViewModel closure tests** — `tests/LifeGrid.Application.Tests/WeeklyHabits/WeeklyHabitsViewModelClosureTests.cs`
- Note: these test computed-property logic in isolation (the ViewModel is Presentation-layer; test the logic as a pure function or via the handler that feeds the DTO, not the MAUI VM itself)
- Better approach: test `CloseWeekCommand` handler + the DTO-to-VM mapping logic as a computed utility; or test via Application layer helpers
- Alternative: move `IsCloseWeekButtonVisible` logic into a query response field — `GetWeeklyHabitsQueryHandler` could compute `IsCloseable` and `IsClosed` booleans in the DTO itself, making them testable at the Application layer

**F5. Application — `NotificationRouteParser` additions** — extend existing `NotificationRouteParserTests.cs`
- `WeekDeepLink_ReturnsWeekDetailRoute`
- `SummaryDeepLink_ReturnsWeekSummaryRoute`

**F6. Infrastructure — `WeekRepository.GetByStartDateAsync`** — extend `WeekRepositoryTests.cs`
- `GetByStartDate_ExistingWeek_ReturnsCorrectEntity`
- `GetByStartDate_MissingWeek_ReturnsNull`

---

## Estimated Test Count

| Layer | Before | New | After |
|---|---|---|---|
| Domain | 111 | 2 | 113 |
| Application | 167 | 10 | 177 |
| Infrastructure | 72 | 2 | 74 |
| **Total** | **350** | **14** | **364** |

---

## EF Migration

No migration required. `WeekStatus` is stored as a string via `HasConversion<string>()`. Adding `Closed` as a new enum value produces no schema change.

---

## Risk & Open Questions

| Risk | Mitigation |
|---|---|
| WorkManager exact NuGet package name for .NET 10 MAUI | Confirm `Xamarin.AndroidX.Work.Runtime` at implementation time; fallback to raw Android `JobScheduler` if binding is unavailable |
| `Plugin.LocalNotification` Android 13+ POST_NOTIFICATIONS permission | Add `<uses-permission android:name="android.permission.POST_NOTIFICATIONS" />` to `AndroidManifest.xml` and request at runtime on first worker execution |
| ViewModel closure logic tests (MAUI VM can't be unit tested without MAUI runtime) | Either (a) extract the boolean logic into the DTO / Application layer query response, or (b) test via a pure C# helper method extracted from the VM |
