# LifeGrid - Phase 19 Vertical Slice Requirements
## Goal Selection & Timeline Filtering Architecture

This document specifies the structural and technical requirements for Phase 17. The objective is to implement interactive goal selection (single and multi-select) from the Goals view, driving a dynamically filtered state within the Timeline view as dictated by `docs\specs\functional-requirements.md` section 4.2.3.

---

## 1. External Reference Mapping
Claude Code must parse visual and functional constraints directly from the master repository definitions:
* **Functional Logic:** `docs\specs\functional-requirements.md` (Section 4.2.3)
* **Design System & Icons:** `docs\specs\style-guide.md` (2px corner radius, standard system icons like `check_box`)
* **Navigation Architecture:** `docs\specs\navigation-architecture.md`
* **Data Schema:** `docs\specs\data-structure.json` (`Week` and `Week_Goal_Item`)

---

## 2. Domain & Application Layer (Query Architecture)

### 2.1 Dynamic Timeline Query Expansion
* Modify the existing `GetTimelineQuery` (from Phase 13) to accept an optional parameter: `List<Guid> FilterGoalIds`.
* **Filtering Logic:** 
  * If `FilterGoalIds` is null or empty, return the full, unfiltered chronological timeline.
  * If populated, filter the nested `Week_Goal_Items` collection inside each `Week` to include *only* items matching the provided Goal IDs.
  * **Exclusion Rule:** If a `Week` aggregate contains zero matching `Week_Goal_Items` after the filter is applied, that entire `Week` must be omitted from the query result.

---

## 3. Presentation Layer (Goals List View)

### 3.1 Interaction Modes & Gestures
Introduce a state machine to the Goals View model handling two distinct interaction modes: `Standard` and `MultiSelect`.

* **Single-Tap (Standard Mode):**
  * Tapping a Goal Card instantly routes the user to the `TimelineView`.
  * Passes the single tapped `GoalID` as the active filter parameter.
* **Long-Press (Enters MultiSelect Mode):**
  * Long-pressing any Goal Card transitions the view into `MultiSelect` mode and immediately marks the pressed goal as selected.
  * While in `MultiSelect` mode, subsequent single-taps toggle the selection state of the target goal instead of navigating.

### 3.2 Visual Selection State
* When a goal is selected, render a clear visual indicator on the Goal Card: a square checked box icon (`check_box`) utilizing the primary accent color.

### 3.3 Multi-Select Action Controller
* When `MultiSelect` mode is active and at least one goal is selected, dynamically render a persistent action button at the absolute bottom of the Goals List container (sitting flush above the Ad Space/Bottom Navigation).
* **Label:** `"View Filtered Timeline"`
* **Action:** Clicking this routes the user to the `TimelineView`, passing the array of selected `GoalID`s as the filter parameter, and resets the Goals View back to `Standard` mode.

---

## 4. Presentation Layer (Timeline View)

### 4.1 Filtered State & UI Injection
* When the Timeline View initializes, check if `FilterGoalIds` parameters were passed in the navigation payload.
* Set a local UI state flag: `IsFilteredMode`.

### 4.2 The "See all Goals" Action Hook
* If `IsFilteredMode` evaluates to `true`, dynamically render a persistent bottom action button pinned below the scrolling list.
* **Label:** `"See all Goals"` (Utilizing standard button styling with 2px corner radius).
* **Execution Rule:** Clicking this button must:
  1. Clear the active `FilterGoalIds` array.
  2. Re-execute the `GetTimelineQuery` to fetch the complete dataset.
  3. Set `IsFilteredMode` to `false`, subsequently hiding the `"See all Goals"` button.
  4. Ensure the UI updates asynchronously without breaking the scrolling context.

---

## 5. Test-Driven Development (TDD) Invariants

* **Query Filtering Verification:** Write a unit test passing a specific `GoalID` to the modified `GetTimelineQuery`. Assert that the returned `Week` aggregates contain strictly `Week_Goal_Items` matching that ID, and that weeks with zero matching items are mathematically excluded from the root list.
* **Selection State Assertions:** Write a view model test simulating a Long-Press command. Assert that the view model transitions to `MultiSelect` mode, adds the target ID to the selection array, and sets the `"View Filtered Timeline"` button visibility flag to `true`.
* **Filter Reset Verification:** Write a view model test for the Timeline View asserting that executing the `"See all Goals"` command properly nullifies the filter array, triggers a full data reload, and hides the filter reset button.