# LifeGrid - Phase 13 Vertical Slice Requirements
## Timeline View Baseline & Weekly Card Architecture

This document specifies the strict structural, visual, and technical requirements for Phase 13. The objective is to wire up the Timeline navigation node and construct the baseline global Timeline view, focusing on a highly fluid, engaging scrolling experience populated with summarized week-level data.

---

## 1. External Reference Mapping
Claude Code must parse visual references, layout constraints, and data definitions directly from the master repository files:
* **Functional Logic:** `docs\specs\functional-requirements.md` (Section 2.1)
* **Visual & Interaction Tokens:** `docs\specs\assets\wireframes\wf7.png` (Specifically targeting the fluid/stacking scrolling physics and card layout)
* **Design System Baseline:** `docs\specs\style-guide.md` (Colors, typography, corner radii) and `docs\specs\screen-layout-specification.md` (Viewport boundaries)
* **Navigation Architecture:** `docs\specs\navigation-architecture.md` (Routing from the bottom menu)
* **Data Schema:** `docs\specs\data-structure.json` (`Week` and `Week_Goal_Item` objects)

---

## 2. Domain & Application Layer (Query Architecture)

### 2.1 Timeline Aggregation Service
* Implement a query handler (e.g., `GetTimelineQuery`) to retrieve the chronological sequence of `Week` aggregates.
* **Relational Mapping:** The handler must traverse the `GoalID` within each `Week_Goal_Item` to query the `Goal` aggregate and project the raw text `Goal.Description`.
* **Order:** The weeks should be presented in strict chronologial order


### 2.2 Strict Data Payload Mapping
The payload must strictly project the following structural tree for the UI to consume:
* **`Week` Entity Root:**
  * `Start_Date` (DateTime)
  * `Status` (String: Active, Hibernated, Frozen)
  * `Global_Metrics.Total_Weekly_SP_Earned` (Integer)
* **`Week_Goal_Items` Array (Nested within Week):**
  * `Goal.Description` (String, resolved via Goal ID)
  * `Penalty_State` (String: Clean, Level_1_Warning, Probation_Week_2, Reckoning_Week_3)
  * `Metrics.Goal_Weekly_GP` (Float)
  * `Metrics.Goal_Weekly_XP_Earned` (Integer)

---

## 3. Presentation Layer (MAUI UI Injection)

### 3.1 Routing & Navigation Lock
* Activate the `"Timeline"` node in the bottom navigation menu (defined in `docs\specs\navigation-architecture.md`).
* Tapping this node must safely route the Main Interaction Area to the new `TimelineView`.

### 3.2 Scrolling Physics & Experience (`docs\specs\assets\wireframes\wf7.png` targeting)
* **Scrolling Mechanics:** Implement a visually engaging vertical scroll experience (`CollectionView` or specialized layout). The scrolling must feel premium and fluid, mimicking the overlapping, snapping, or dynamic spacing behaviors implied by `docs\specs\assets\wireframes\wf7.png`.
* **Viewport Restraints:** Ensure the scrollable list strictly adheres to the boundaries defined in `docs\specs\screen-layout-specification.md`, sitting comfortably between the global HUD and the Ad/Bottom Navigation zones.

### 3.3 The Week Summary Card Architecture
Each item in the scrollable timeline is a distinct, elevated Card representing a single `Week`, adhering to `docs\specs\style-guide.md` (forced light mode, 2px corner radius, specific typography).

#### Zone A: Week Header
* **Top Left:** Formatted `Start_Date` (e.g., "Week of Jun 21") using the primary font.
* **Top Right:** `Status` and `Total_Weekly_SP_Earned` (e.g., "Active | SP: 15"). Use secondary monospace font for numerical highlighting.

#### Zone B: Nested Goal Items (List)
* Within the Week Card, render a visually distinct sub-list iterating through the `Week_Goal_Items`.
* For each item, display:
  * **Description:** The resolved `Goal.Description`.
  * **Penalty State Indicator:** Apply conditional styling based on `Penalty_State` (e.g., standard text for Clean, warning accent colors for Probation/Reckoning).
  * **Metrics Row:** Render specific performance data side-by-side using the secondary font: `GP: {Goal_Weekly_GP} | XP: {Goal_Weekly_XP_Earned}`.

---

## 4. Test-Driven Development (TDD) Invariants

* **Relational Query Verification:** Write a unit test for the Timeline Query ensuring that it successfully resolves the `GoalID` into the correct textual `Description`. 
* **UI Routing Verification:** Assert that the Timeline bottom menu icon successfully executes the navigation command to load `TimelineView` into the main viewport context.
* **Data Binding Assertions:** Implement view model tests verifying that the nested collection of `Week_Goal_Items` accurately binds to its parent `Week` object without data bleed between adjacent weeks.