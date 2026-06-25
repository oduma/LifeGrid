# LifeGrid - Phase 30 Vertical Slice Requirements
## Weekly Lifecycle Engine & Closure Protocol

This document defines the strict requirements for Phase 30. The objective is to implement the temporal lifecycle of a `Week`, enforcing the manual and automatic closure protocols as defined in `functional-requirements.md` (Section 5.1). For this phase, complex procrastination/review math is deferred; the focus is strictly on state transitions, background scheduling, notifications, and the read-only summary UI.

---

## 1. External Reference Mapping
Claude Code must parse structural rules and UI patterns directly from the master repository definitions:
* **Functional Logic:** `functional-requirements.md` (Section 5.1).
* **Data Schema:** `data-structure.json` (`Week.Status` must now explicitly support `"Closed"`).
* **Pre-existing Architectures:** Phase 20 (`WeeklyHabitsView` for details layout) and Phase 29 (`NotificationInbox` for deep-linking).

---

## 2. Domain & Application Layer (Lifecycle Engine)

### 2.1 Week Closure Command
* Implement the `CloseWeekCommand` handler.
* **Payload:** `WeekID` (Guid).
* **Mutation:** Retrieve the target `Week` entity and update `Week.Status` from `"Active"` (or other states) to `"Closed"`.
* **Constraint:** For Phase 30, explicitly *stub out* or bypass the gamification/procrastination penalty calculations. Just commit the status change to the SQLite database via an atomic transaction.

### 2.2 Background Scheduling Service (Temporal Triggers)
Implement a platform-native background worker (e.g., via Android `WorkManager` or a MAUI Background Service equivalent) that evaluates the temporal state of historical weeks.

* **Trigger 1: The Monday 9 AM Prompt**
  * **Condition:** Every Monday at 9:00 AM local time, check the database for the *immediately preceding* week (the week that ended the day before, on Sunday).
  * **Action:** If that week is NOT `"Closed"`, push a Notification (via Phase 29 `INotificationService`).
  * **Payload:** Title: `"Week Ended"`, Message: `"Please review and close your previous week."`, `DeepLinkUrl`: `"lifegrid://week/{PreviousWeekID}"`.

* **Trigger 2: The Wednesday 9 AM Auto-Close**
  * **Condition:** Every Wednesday at 9:00 AM local time, check the database for the week that ended the *previous* Sunday.
  * **Action:** If the `Week.Status` is still NOT `"Closed"`:
    1. Automatically execute the `CloseWeekCommand` for that `WeekID`.
    2. Push a Notification (via Phase 29 `INotificationService`).
    3. **Payload:** Title: `"Week Auto-Closed"`, Message: `"Your previous week was automatically closed by the system."`, `DeepLinkUrl`: `"lifegrid://summary/{PreviousWeekID}"`.

---

## 3. Presentation Layer (MAUI UI Injection)

### 3.1 Weekly Habits View Extensions (The Pending Closure State)
Modify the existing `WeeklyHabitsView` (Phase 20) to react to historical temporal contexts.

* **Late Logging:** If the user deep-links into this view for a past week that is still `"Active"` (between Monday 12:00 AM and Wednesday 8:59 AM), standard Phase 22 habit logging remains enabled.
* **The Close Action:** If the viewed week's `End_Date` has passed and `Status != "Closed"`, dynamically render a prominent, primary-colored action button at the top or bottom of the view: `"Close the Week"`.
* **Execution:** Tapping this button fires the `CloseWeekCommand`, updates the local UI state to `"Closed"`, and reveals the Summary link.

### 3.2 The Closed State & Routing
* If a user navigates to a `Week` where `Status == "Closed"` (either manually or automatically), the view must lock down.
* **UI Lockdown:** Hide the `"Close the Week"` button. Disable all habit logging tap-gestures.
* **The Summary Hook:** Render a new primary action button prominently at the top of the screen: `"Go to Week Summary"`. Tapping this routes the user to the `WeekSummaryView`.

### 3.3 Week Summary View (Phase 30 Baseline)
* Implement a new dedicated page: `WeekSummaryView`.
* **Baseline Display:** For Phase 30, this screen is a strict, read-only replication of the data already presented in the `WeeklyHabitsView`.
* **Data Binding:** It must display the `Week` details (`Start_Date`, `Status`, `Global_Metrics`) and iterate through all `Week_Goal_Items` and nested `Habits`.
* **Interaction:** Strictly disable all forms of data entry, logging, or state mutation on this view.

---

## 4. Test-Driven Development (TDD) Invariants

* **Status Mutation Verification:** Write a unit test asserting that executing `CloseWeekCommand` successfully updates the target `Week.Status` to `"Closed"` in the database.
* **Background Auto-Close Logic:** Write a domain test that mocks the system clock to a Wednesday at 9:01 AM. Assert that the background service evaluates an unclosed previous week and successfully fires both the `CloseWeekCommand` and the `PushNotification` method.
* **UI State Toggle Assertions:** Write view model tests for `WeeklyHabitsView` asserting that:
  * `IsCloseWeekButtonVisible` evaluates to `true` when the week is past its end date but still Active.
  * `IsSummaryButtonVisible` evaluates to `true` and logging functions are disabled when the week's status is `"Closed"`.