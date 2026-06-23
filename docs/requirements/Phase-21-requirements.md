# LifeGrid - Phase 21 Vertical Slice Requirements
## The Home View: Current Week Active Dashboard

This document specifies the structural and technical requirements for Phase 21. The objective is to wire up the application's root landing page (`HomeView`), configuring it to automatically resolve the user's current temporal context and display the active Weekly Habit Dashboard.

---

## 1. External Reference Mapping
Claude Code must parse visual references, layout constraints, and data definitions directly from the master repository files:
* **Navigation Architecture:** `docs\specs\navigation-architecture.md` (Home routing node)
* **Visual Layout:** Inherits the structural blueprint established in Phase 20 (`WeeklyHabitsView`) and `wf8.png`.
* **Design System Baseline:** `docs\specs\style-guide.md` and `docs\specs\screen-layout-specification.md`

---

## 2. Domain & Application Layer (Query Architecture)

### 2.1 Current Week Resolution Service
* Implement a routing query handler (e.g., `GetCurrentWeekHabitsQuery`).
* **Temporal Math Logic:** The handler must evaluate `DateTime.UtcNow` (or local equivalent) and mathematically determine the current active `Week`. (Recall Phase 10 rules: Weeks are Monday-anchored).
* **Data Retrieval:** Once the current `WeekID` is resolved, execute the exact same nested data aggregation logic built in Phase 20 (`Week` -> `Week_Goal_Items` -> `Habits`).
* **No Goal Filtering:** Unlike the Timeline drill-down, the Home View must default to an unfiltered state, displaying all active goals for the current week.

---

## 3. Presentation Layer (MAUI UI Injection)

### 3.1 Routing & Navigation Binding
* Activate the `"Home"` node in the bottom navigation menu (defined in `navigation-architecture.md`).
* Configure the application's startup routing shell to default to the `HomeView` immediately after the Phase 8 Global HUD initializes and the Phase 2 Onboarding check resolves to `IsComplete == true`.

### 3.2 View Layout Architecture
The `HomeView` structurally mirrors the Phase 20 `WeeklyHabitsView`, but acts as a root tab.

* **Zone A: Global Week Header:** Display the overarching `Start_Date` (e.g., "Current Week"), `Status`, and `Total_Weekly_SP_Earned`.
* **Zone B: Goal Grouping Blocks:** Iterate through the `Week_Goal_Items` to group active habits by their parent goal.
* **Zone C: Habit Cards:** Render the interactive habit cards exactly as specified in Phase 20.

### 3.3 The Empty State (Crucial Home Context)
Because `HomeView` is the landing page, it must handle the scenario where the user has zero active goals for the current temporal week.
* **Condition:** If the `GetCurrentWeekHabitsQuery` returns null or an empty goals array.
* **Layout:** Clear the Main Interaction Area and render a center-aligned empty state message utilizing the secondary typography token.
* **Message:** `"No active goals or habits scheduled for this week."`
* **Call to Action:** Render a primary button labeled `"Create a Goal"` that safely routes the user to the Phase 7 `Create Goal` view.

---

## 4. Test-Driven Development (TDD) Invariants

* **Temporal Resolution Verification:** Write a unit test that mocks `DateTime.UtcNow` to a specific Wednesday and asserts that the `GetCurrentWeekHabitsQuery` successfully queries the database for the `Week` entity anchored to the preceding Monday.
* **Empty State UI Binding:** Write a view model test asserting that when zero goals are active for the current week, the `IsWeeklyDataVisible` flag is `false`, and the `IsEmptyStateVisible` flag evaluates to `true`.
* **Root Navigation Assert:** Assert that tapping the Home tab does not push a new page onto the navigation stack if the user is already on the Home view, preventing memory leaks.