# LifeGrid - Phase 20 Vertical Slice Requirements
## Weekly Habit Dashboard & Filtered Drill-down

This document specifies the structural and technical requirements for Phase 20. The objective is to construct the detailed weekly drill-down view (`WeeklyHabitsView`) accessed from the Timeline, correctly rendering week metrics, goal-specific metrics, and the granular list of habits while strictly inheriting any active goal filters.

---

## 1. External Reference Mapping
Claude Code must parse visual references, layout constraints, and data definitions directly from the master repository files:
* **Functional Logic:** `docs\specs\functional-requirements.md` (Section 2.3)
* **Visual & Interaction Tokens:** `docs\specs\assets\wireframes\wf8.png` (Specifically targeting the habit list grouping and scrolling layout)
* **Design System Baseline:** `style-guide.md` and `docs\specs\screen-layout-specification.md`
* **Data Schema:** `docs\specs\data-structure.json` (`Week`, `Week_Goal_Item`, and `Habit` objects)

---

## 2. Domain & Application Layer (Query Architecture)

### 2.1 Weekly Habits Aggregation Service
* Implement a new query handler (e.g., `GetWeeklyHabitsQuery`).
* **Input Parameters:** 1. `WeekID` (Guid - required)
  2. `FilterGoalIds` (List<Guid> - optional, inherited from Phase 17)
* **Relational Mapping & Filtering:**
  * Fetch the root `Week` matching the `WeekID`.
  * Fetch the `Week_Goal_Items` for that week. If `FilterGoalIds` is populated, strictly filter this list to only include matching goals.
  * For each resolved `Week_Goal_Item`, fetch the associated `Habit` entities active for this week.

### 2.2 Strict Data Payload Mapping
The payload must project the following structural tree for the UI:
* **`Week` Entity Root:**
  * `Start_Date` (DateTime)
  * `Status` (String)
  * `Global_Metrics.Total_Weekly_SP_Earned` (Integer)
* **`Week_Goal_Items` Array (Filtered):**
  * `Goal.Description` (Resolved from Goal ID)
  * `Week_Goal_Number` (Integer)
  * `Penalty_State` (String)
  * `Metrics.Goal_Weekly_GP` (Float)
  * `Metrics.Goal_Weekly_XP_Earned` (Integer)
  * **`Habits` Array (Nested inside the Goal Item):**
    * `Habit_Type` (String: Planned, Moment Burst, Flash)
    * `Habit_Name` (String)
    * `Habit_Description` (String)
    * `Target` { `Target_Value`, `Measurement_Unit`, `Deadline_DateTime` }
    * `Completed_Values_Log` Array { `Actual_Value`, `Measurement_Unit`, `Proof_Text`, `Proof_Image_URL`, `Timestamp` }

---

## 3. Presentation Layer (MAUI UI Injection)

### 3.1 Routing & Filter Inheritance
* Tapping a Week Card in the `TimelineView` must execute a navigation push to `WeeklyHabitsView`.
* The navigation parameter payload must include both the `WeekID` and the currently active `FilterGoalIds` array (if any) to ensure the user's timeline filter persists perfectly into the drill-down view.

### 3.2 View Layout Architecture (`wf8.png` targeting)
Within the Main Interaction Area, construct a vertical scrolling layout:

#### Zone A: Global Week Header
* **Top Component:** A fixed or sticky header displaying the overarching `Start_Date`, `Status`, and `Total_Weekly_SP_Earned`.

#### Zone B: Goal Grouping Blocks
* Iterate through the `Week_Goal_Items`. For each item, render a distinct visual grouping:
  * **Goal Sub-Header:** Render the `Goal.Description` alongside the relative `Week_Goal_Number` (e.g., "Week 4").
  * **Goal Metrics:** Display the `Penalty_State` (using warning color tokens if in Probation/Reckoning) and the active `GP` and `XP` for that specific goal.

#### Zone C: Habit Cards (Nested under Zone B)
* For each habit within the goal group, render a standard Habit Card (2px corner radius, Surface color):
  * **Header:** `Habit_Name` and an indicator for `Habit_Type`.
  * **Body:** `Habit_Description`.
  * **Target Block:** Muted text showing the target value, unit, and deadline.
  * **Completion Log:** A nested, compact sub-list of `Completed_Values_Log` entries.
  * **Proof Image Handling:** If a `Proof_Image_URL` exists in a log entry, render a standard system image icon (`image` or `photo_camera`). Tapping this icon triggers an overlay/modal to display the expanded image. If the URL is null, hide the icon.

---

## 4. Test-Driven Development (TDD) Invariants

* **Filter Propagation Verification:** Write a view model navigation test asserting that tapping a week card while `IsFilteredMode` is true successfully passes the active `FilterGoalIds` array to the `WeeklyHabitsView` parameters.
* **Query Payload Assertions:** Write a unit test for the `GetWeeklyHabitsQuery` asserting that `Habit` entities are correctly grouped under their parent `Week_Goal_Item` and that habits belonging to filtered-out goals are strictly excluded from the returned JSON/Object tree.
* **Image Icon State Test:** Write a UI/Binding test asserting that the Proof Image icon's `IsVisible` property correctly evaluates to `false` when `Proof_Image_URL` is null or empty.