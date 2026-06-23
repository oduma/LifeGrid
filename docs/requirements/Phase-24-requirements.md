# LifeGrid - Phase 24 Vertical Slice Requirements
## Ancillary Actions: Vice Survey Hook & Timeline Pausing

This document specifies the structural and technical requirements for Phase 24. The objective is to enable two specific ancillary actions: exposing the Bad Habits Survey trigger directly within the Goals view (as a once-off action), and implementing the Week Pause mechanics (Hibernate/Freeze) directly on the Timeline view's weekly summary cards.

---

## 1. External Reference Mapping
Claude Code must parse visual and functional constraints directly from the master repository definitions:
* **Functional Logic:** `functional-requirements.md` (Section 4.1.1 for Vice Survey, Section 4.3.2 for Pause Actions).
* **Existing Architectures:** Phase 9 (Vice Survey Pipeline) and Phase 13 (Timeline View).
* **Data Schema:** `data-structure.json` (`UserProfile.IsViceSurveyCompleted`, `Week.Status`).

---

## 2. Domain & Application Layer (Action Handlers)

### 2.1 The Vice Survey Hook (Reusability)
* **Command Re-use:** Do not duplicate the survey logic. Re-use the existing `LaunchViceSurveyCommand` built in Phase 9.
* **State Verification:** The application layer must ensure that querying `UserProfile.IsViceSurveyCompleted` dictates the availability of this command, maintaining its strict single-use lifecycle.

### 2.2 Timeline Pause Handler (Hibernate/Freeze)
* Implement a new command handler (e.g., `PauseWeekCommand`).
* **Input Parameters:** `WeekID` (Guid) and `PauseType` (Enum/String: `Hibernated` or `Frozen`).
* **Domain Mutation:** * Retrieve the target `Week` entity.
  * Mutate `Week.Status` to the provided `PauseType`.
  * **Rule Constraint:** Ensure any necessary recalculations or shifts for future weeks (if defined by 4.3.2) are executed mathematically, preserving the calendar alignment.
  * Commit the state change to the SQLite database via an atomic transaction.

---

## 3. Presentation Layer (MAUI UI Injection)

### 3.1 Goal View: Vice Survey Action
* Within the main `GoalsView` layout, render a contextual action button or banner: `"Detect Hidden Vices"`.
* **Visibility Binding:** Bind the `IsVisible` property directly to a negated boolean check of `UserProfile.IsViceSurveyCompleted`. If the user has already taken the survey, this UI element must completely collapse and remove itself from the visual tree.
* **Action:** Tapping the button executes the `LaunchViceSurveyCommand`, routing the user safely into the Phase 9 survey flow.

### 3.2 Timeline View: Week Card Pause Actions
* Modify the `Week Card` component in the `TimelineView` (Phase 13) to support contextual actions (e.g., via a swipe menu, long-press, or a subtle "more options" `⋮` icon).
* **Action Menu:** Expose two distinct pause actions:
  * `"Hibernate Week"` (Planned pause)
  * `"Freeze Week"` (Emergency pause)
* **Visual State Update:** Upon executing either command, the UI must asynchronously refresh via `INotifyPropertyChanged`. The `Status` indicator in the top right of the Week Card (Zone A) must instantly change from `Active` to `Hibernated` or `Frozen`, utilizing distinct warning/muted color tokens from `style-guide.md` to visually differentiate paused weeks from active ones.

---

## 4. Test-Driven Development (TDD) Invariants

* **Survey Visibility Verification:** Write a ViewModel test for the Goals View asserting that mocking `IsViceSurveyCompleted = true` results in the action button's visibility flag evaluating strictly to `false`.
* **Pause Command Persistence:** Write an integration test asserting that executing `PauseWeekCommand` with a `Frozen` parameter successfully updates the SQLite database record for the target `Week` without modifying the timestamps or target values of its nested habits.
* **UI Reactivity Assert:** Verify that dispatching the Pause command successfully raises the property changed event for the Week Card's Status string, ensuring the UI updates without requiring a full scroll reload.