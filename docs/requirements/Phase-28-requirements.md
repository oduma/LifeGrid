# LifeGrid - Phase 28 Vertical Slice Requirements
## Shield Points (SP) Economy Integrity & Reconciliation

This document specifies the technical requirements for Phase 28. The objective is to implement the automated SP Integrity Check (per `functional-requirements.md` 4.3.3) triggered reactively by any mutation to the `UserProfile.Economy.Current_SP` balance.

---

## 1. External Reference Mapping
* **Functional Logic:** `functional-requirements.md` (Section 4.3.3 - SP Reconciliation Logic).
* **Data Schema:** `data-structure.json` (`UserProfile.Economy.Current_SP`).
* **Architecture Rules:** Phase 23 (Economy State Synchronization & Messaging Bus).

---

## 2. Domain & Application Layer (Reconciliation Engine)

### 2.1 Shield Integrity Monitor
* Implement a domain service (`ShieldIntegrityMonitor`) tasked with validating the Shield Economy state.
* **Trigger Definition:** This service MUST subscribe to the global `EconomyStateMutatedEvent` (as defined in Phase 23).
* **Logic Branching:** Whenever the event indicates a change in `Current_SP`, the monitor must evaluate:
    1. **Boundary Validation:** Ensure `Current_SP` stays within system-defined bounds. If a state change forces SP < 0, the monitor MUST immediately reconcile the balance to 0.
    2. **Integrity Reconciliation (Section 4.3.3):** Evaluate current SP levels against active goal-threats. If SP levels trigger specific 4.3.3 automated adjustment rules (e.g., auto-shielding or penalty mitigation), apply the logic.
    3. **Persistence:** If the monitor modifies the state, it MUST persist the change via an atomic SQLite transaction and publish a new `EconomyReconciledEvent` to notify the application of the updated, reconciled reality.

---

## 3. Reactive Architecture & State Flow

### 3.1 Event-Driven Trigger Chain
To ensure the system is reactive and never polls the database, implement a strict pub/sub flow:
1. **Mutation Source:** Any service (Habit Logging, Penalty Escalation) modifies the `Economy` aggregate.
2. **Publication:** The Economy Service publishes `EconomyStateMutatedEvent`.
3. **Interception:** The `ShieldIntegrityMonitor` intercepts the event, performs the 4.3.3 integrity calculation.
4. **Feedback Loop:** If the integrity check modifies the state, the system emits a secondary `EconomyReconciledEvent`. The UI (HUD and Dashboard) listens for this event to ensure it is always visually consistent with the reconciled domain state.

---

## 4. Test-Driven Development (TDD) Invariants

* **Integrity Trigger Test:** Write a unit test that mocks an SP mutation event. Assert that the `ShieldIntegrityMonitor` catches the event and successfully initiates the evaluation method.
* **Boundary Enforcement Test:** Mock an invalid state (e.g., SP balance set to a negative value via an external command). Assert that the `ShieldIntegrityMonitor` catches the event, calculates the correction to 0, and persists the corrected state.
* **Concurrency/Reactivity Verification:** Ensure that reconciliation logic (which might update the DB) does not cause UI blocking. Write a test asserting that the `ShieldIntegrityMonitor` runs its logic asynchronously and does not block the main execution thread when processing the `EconomyStateMutatedEvent`.