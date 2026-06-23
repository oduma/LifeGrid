# LifeGrid - Phase 22 Vertical Slice Requirements
## Habit Execution & Progress Logging (Preparatory Pipeline)

This document specifies the strict requirements for Phase 22. The objective is to establish the baseline data-entry workflow for habit completion, as defined in `docs\specs\functional-requirements.md` sections 3.1, 3.2, 3.2.1, and 3.2.2. In this preparatory phase, the focus is strictly on data capture, persistence, and UI display; gamification math (XP/GP recalculations) will be wired up in a subsequent phase.

---

## 1. External Reference Mapping
Claude Code must parse visual and functional constraints directly from the master repository definitions:
* **Functional Logic:** `docs\specs\functional-requirements.md` (Sections 3.1, 3.2, 3.2.1, 3.2.2)
* **Data Schema:** `docs\specs\data-structure.json` (`Habit.Completed_Values_Log` array)
* **Design System Baseline:** `docs\specs\style-guide.md` and `docs\specs\screen-layout-specification.md`

---

## 2. Domain & Application Layer (Command Architecture)

### 2.1 Progress Logging Command Handler
* Implement a command handler (e.g., `LogHabitProgressCommand`) responsible for receiving user input and appending it to the domain aggregate.
* **Payload Requirements:**
  * `HabitID` (Guid)
  * `Actual_Value` (Float - User provided)
  * `Measurement_Unit` (String - Inherited from the Habit's Target)
  * `Proof_Text` (String - Nullable, User provided)
  * `Proof_Image_URL` (String - Nullable local URI, User provided)
  * `Timestamp` (DateTime - Automatically generated as `DateTime.UtcNow` at the moment of submission)

### 2.2 Domain State Mutation
* The handler must retrieve the target `Habit` entity from the SQLite database.
* Append the new progress payload as a distinct entry inside the `Completed_Values_Log` collection.
* **Constraint:** For this preparatory phase, do *not* trigger global Economy updates or Penalty evaluations. Strictly persist the log to the database.

---

## 3. Presentation Layer (MAUI UI Injection)

### 3.1 Trigger & Routing
* **Interaction:** Tapping any Habit Card within the `HomeView` (Phase 21) or `WeeklyHabitsView` (Phase 20) must intercept the default tap gesture.
* **Routing:** Launch a modal overlay or bottom sheet (`HabitLoggingBottomSheet`) focused specifically on the tapped habit.

### 3.2 Logging Interface Layout
Construct the logging form utilizing the design system tokens (Light Mode, 2px corner radius, standard padding):
* **Context Header:** Display the `Habit_Name` (Primary font) and the Target requirement (e.g., "Target: 5.0 km").
* **Value Input Group:**
  * A numeric entry field for `Actual_Value`.
  * A static, muted label displaying the `Measurement_Unit` directly adjacent to the input.
* **Proof Input Group (Optional Data):**
  * A multi-line text area for `Proof_Text` (Secondary font).
  * An action button (system icon `add_a_photo`) to attach an image. (For this phase, a simple file picker returning a local URI string is sufficient).
* **Action Controller:** * A primary button labeled `"Log Progress"`.

### 3.3 Asynchronous UI Refresh
* Upon clicking `"Log Progress"`, the view model must dispatch the command, await the database transaction, and dismiss the modal.
* The parent view (`HomeView` or `WeeklyHabitsView`) must dynamically refresh via `INotifyPropertyChanged` to immediately display the newly added log inside the Habit Card's `Completed_Values_Log` sub-list.

---

## 4. Test-Driven Development (TDD) Invariants

* **Command Validation Test:** Write a unit test ensuring that a `LogHabitProgressCommand` cannot be executed if the `Actual_Value` is less than or equal to `0`.
* **Database Persistence Assert:** Write an integration test that creates a mock Habit, submits a progress log command, and queries the SQLite database to assert that the `Completed_Values_Log` collection count has increased by exactly `1` and contains the correct UTC Timestamp.
* **Modal State Verification:** Write a UI/ViewModel test verifying that the modal state securely binds to the specific `HabitID` that was tapped, preventing log data from bleeding into the wrong habit.