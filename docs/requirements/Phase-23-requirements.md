# LifeGrid - Phase 23 Vertical Slice Requirements
## Gamification Engine: Economy Math & Global State Sync

This document specifies the strict technical requirements for Phase 23. The objective is to wire the underlying gamification mathematics into the habit logging pipeline (established in Phase 22) and to ensure that all economy state changes are broadcast dynamically across the entire application viewport stack.

---

## 1. External Reference Mapping
Claude Code must parse the exact calculation formulas, multipliers, and progression constraints directly from the master repository definitions:
* **Functional Logic:** `docs\specs\functional-requirements.md` (Sections 3.4, 3.5, 3.6 for metric scoring, and 3.8, 3.8.1, 3.8.2 for level/SP progression constraints).
* **Data Schema:** `docs\specs\data-structure.json` (`UserProfile.Economy`, `Week_Goal_Item.Metrics`).
* **Architecture Rules:** `docs\specs\TECHNICAL_STANDARDS.md` (For async state propagation and messaging patterns).

---

## 2. Domain Layer (Gamification Core Engine)

### 2.1 The Economy Service Module
Implement a pure, isolated C# domain service (e.g., `GamificationCalculationEngine`) responsible for crunching the outcome of a newly logged habit transaction.

* **Metric Resolution (Sections 3.4, 3.5, 3.6):** * Compare the newly submitted `Actual_Value` against the parent `Habit.Target.Target_Value`.
  * Calculate the specific `GP` (Goal Points), `XP` (Experience Points), and `SP` (Shield Points) earned based on the specific habit type (`Planned`, `Moment Burst`, `Flash`) rules outlined in the functional requirements.
* **Level Progression Constraint (Sections 3.8, 3.8.1, 3.8.2):**
  * **Temporary Hardcoded Threshold:** For Phase 23, the XP threshold to advance to the next level is strictly hardcoded to **300 XP**.
  * **Rollover Math:** When new XP is added to the profile, the engine must calculate rollovers mathematically. *(Example: If a user is at 280 XP toward the next level and earns 50 XP, `UserProfile.Current_Level` must increment by 1, and the current level progress bucket carries over a remainder of 30 XP).*

### 2.2 Atomic Persistence Implementation
* Refactor the `LogHabitProgressCommand` from Phase 22.
* After appending the log to the `Habit`, immediately invoke the `GamificationCalculationEngine`.
* Apply the returned metric mutations to both the specific `Week_Goal_Item.Metrics` block and the root `UserProfile.Economy` block.
* Commit all modified entities to SQLite in a **single atomic transaction**. If any calculation fails, the entire log entry must roll back to preserve economic integrity.

---

## 3. Application & Presentation Layer (Global Reactivity)

### 3.1 Global State Synchronization
To satisfy the requirement that GP, XP, and SP update *everywhere* as soon as they change, you must implement a globally decoupled notification system. The app cannot rely on hard page reloads.

* **Implementation Pattern:** Utilize an Event Aggregator (like the `WeakReferenceMessenger` in MAUI Community Toolkit) or an injected global state container (`UserEconomyStateService`) implementing robust `INotifyPropertyChanged` flows.
* **Broadcast Hook:** Upon a successful database commit in the logging command, immediately publish an `EconomyStateMutatedEvent` containing the fresh data.
* **Subscriber Hooks:**
  * **Global HUD (Phase 8):** Must listen for the event and instantly update the `n1-n9` text bindings in the top layout.
  * **Home View Dashboard (Phase 21):** Must listen and instantly update the specific Goal GP/XP metrics visible on the active habit cards.
  * **Timeline View (Phase 13):** Must dynamically reflect the new aggregated metric summaries if the user swipes back to the timeline.

---

## 4. Test-Driven Development (TDD) Invariants

* **Progression Rollover Verification:** Write robust domain unit tests for the `GamificationCalculationEngine`. 
  * Mock a profile sitting at `Level 1` with `290 / 300 XP`. Pass a simulated log worth `25 XP`. 
  * Explicitly assert that the resulting Level is `2` and the leftover progress XP is perfectly calculated at `15`.
* **Type-Specific Scoring Checks:** Implement isolated tests asserting that the point yields from Sections 3.4, 3.5, and 3.6 are properly routed and calculated depending on the enum type (`Planned` vs. `Flash`).
* **Event Propagation Mocking:** Write an integration test ensuring that executing the `LogHabitProgressCommand` successfully fires exactly one `EconomyStateMutatedEvent` message payload.