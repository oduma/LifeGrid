# LifeGrid - Phase 26 Vertical Slice Requirements
## "Showing Up" Gamification: Daily Login Streaks & Badge Logic

This document defines the strict requirements for Phase 26. The objective is to implement the daily login tracking engine, establish the "Showing Up" badge criteria (Bronze, Silver, Gold), and ensure the persistent, once-in-a-lifetime award logic is integrated into the application startup sequence.

---

## 1. External Reference Mapping
Claude Code must parse structural rules, visual tokens, and functional definitions directly from the master repository:
* **Functional Logic:** `docs\specs\functional-requirements.md` (Points 3.10, 3.11 for streak thresholds and display text).
* **Visual Tokens (Color/Size/Icon):** `docs\specs\style-guide.md` (Color tokens for Gold `#FFC300`, Silver `#9CA3AF`, Bronze `#D47A43`) and Google Material Icon: `event_available`.
* **Data Schema:** `docs\specs\data-structure.json` (`LoginHistory` table, `EarnedBadges` table).

---

## 2. Domain & Infrastructure Layer (State & Persistence)

### 2.1 Login Tracking Engine
* **Login History Table:** Implement a `LoginHistory` table in SQLite to track every successful app launch:
  * `ID` (Guid)
  * `Timestamp` (DateTime - UTC)
* **Start-up Orchestrator:** Upon application initialization (in the `App.xaml.cs` or `AppShell` startup routine), execute the `RecordLoginCommand`.
  * Create a new `LoginHistory` entry.
  * Trigger the `EvaluateConsistencyBadgeService` to check if a new badge is earned.

### 2.2 Once-in-a-Lifetime State Persistence
* **Earned Badge Table:** Implement a persistent `EarnedBadges` table:
  * `Badge_ID` (Unique Identifier)
  * `Badge_Icon_Name` (Fixed value: `event_available`)
  * `Badge_Tier` (Enum/String: Bronze, Silver, Gold)
  * `Date_Achieved` (DateTime)
* **Locking Mechanism:** Before awarding a badge, the `BadgeService` must query the `EarnedBadges` table. If the specific tier has already been awarded, do NOT re-award it (ensuring the "once-in-a-lifetime" constraint).

---

## 3. Gamification Logic (Badge Math)

### 3.1 Tiered Badge Definition
The consistency logic must evaluate the login streak and grant awards based on cumulative attendance:
* **Tier Bronze:** Awarded upon hitting the "Starter" streak milestone (as defined in `docs\specs\functional-requirements.md` 3.10).
* **Tier Silver:** Awarded upon hitting the "Consistent" streak milestone.
* **Tier Gold:** Awarded upon hitting the "Master" streak milestone.

### 3.2 Simultaneous Award Handling
* The evaluation logic must be **non-blocking/cumulative**. 
* **Scenario:** If a user returns after a long absence and their login history creates a streak that crosses multiple thresholds simultaneously (e.g., they hit both the Bronze and Silver thresholds at once), the engine must instantiate and persist records for ALL applicable badges in a single transaction.

---

## 4. Presentation Layer (UI & Feedback)

### 4.1 Notification & Display
* **Badge Unlock Notification:** If new badges are earned during the startup routine, trigger a non-intrusive, high-fidelity overlay modal (or Toast notification) immediately after the main dashboard renders.
* **Badge UI Layout:**
  * **Icon:** Render `event_available` (Material Symbol) using the `FontImageSource` approach.
  * **Color:** Dynamically bind the `Color` property to the Tier-specific hex code defined in `docs\specs\style-guide.md` (`Tier-Gold`, `Tier-Silver`, or `Tier-Bronze`).
  * **Caption:** Display the text defined in `docs\specs\functional-requirements.md` (3.10) for the badge, followed by the formatted `Date_Achieved` (e.g., "Achieved: 24 Jun 2026").

### 4.2 Factory Reset Reset
* The `FactoryResetCommand` (from Phase 7) must be updated to include the clearing of the `EarnedBadges` and `LoginHistory` tables, effectively re-enabling the possibility to earn these badges from scratch.

---

## 5. Test-Driven Development (TDD) Invariants

* **Streak Calculation Test:** Write a unit test that provides a mocked `LoginHistory` list spanning multiple scenarios. Assert that if the history satisfies both Bronze and Silver criteria, the service returns two distinct badge objects.
* **Persistence Integrity Test:** Write an integration test asserting that once a badge is persisted to the `EarnedBadges` table, subsequent calls to the calculation engine result in zero *duplicate* database inserts for that same tier.
* **Startup Sequence Verification:** Ensure that the `RecordLoginCommand` and `EvaluateConsistencyBadgeService` are executed as an atomic unit during startup so that if the DB write fails, the streak logic is not updated.