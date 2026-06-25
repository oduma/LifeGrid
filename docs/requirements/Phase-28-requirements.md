# LifeGrid - Phase 28 Vertical Slice Requirements
## Shield Economy (SP) & Integrity Reconciliation Engine

This document specifies the structural and technical requirements for Phase 28. The objective is to implement the reactive SP-to-Shield conversion logic, enforce inventory caps, and integrate the integrity deterrent (cheating penalties) into the domain model.

---

## 1. External Reference Mapping
Claude Code must parse structural rules and logic chains from the following definitions:
* **Functional Logic:** * Point 3.5 from `docs\specs\functional-requirements.md`(Integrity Deterrent & Deficit Mechanics)
  * Point 4.3.3 `docs\specs\functional-requirements.md` (SP to Shield Conversion & Inventory Caps)
* **Data Schema:** `docs\specs\data-structure.json` (`UserProfile.Economy`).

---

## 2. Domain & Application Layer (Calculation Engine)

### 2.1 SP Mutation & Reconciliation Service
Implement a `ShieldEconomyEngine` domain service. This service must act as the single gatekeeper for all `Current_SP` mutations.

* **Reactive Trigger:** This service MUST be triggered whenever `UserProfile.Economy.Current_SP` is modified (via any command: Reward, Penalty, or Daily Login).
* **Mutation Pipeline:**
    1. After the calculations on the Current_SP currently in place
    2. **Shield Conversion (4.3.3):**
       * IF `Current_SP` >= 30:
         * Check `UserProfile.Max_Shield_Cap` (2, or 3 if `OptionalSurveyCompleted` == true).
         * IF `Current_Shield_Count` < `Max_Shield_Cap`:
            * `Current_Shield_Count` += 1
            * `Current_SP` -= 30
       * IF `Current_Shield_Count` == `Max_Shield_Cap`:
         * Do not allow conversion. Cap `Current_SP` at 30 until the user uses a shield (this prevents loss of progress).

### 2.2 Atomic Persistence
* All mutations (SP update, Shield increment, Deficit status) MUST be committed to SQLite within a single atomic transaction to maintain economic integrity.

---

## 3. Presentation Layer (Global HUD Updates)

### 3.1 Reactive UI Synchronization
* As established in Phase 23, the HUD must listen to the `EconomyStateMutatedEvent`.
* When the `ShieldEconomyEngine` completes a conversion or applies a penalty, the event MUST carry the updated `Current_SP` and `Current_Shield_Count`.
* The UI must reflect the "Deep Deficit" state visibly (e.g., changing the SP counter color to the `Error` hex code `#FFFF1B77` when the balance is negative).

---

## 4. Test-Driven Development (TDD) Invariants

* **Cheating Deterrent Test:** Write a unit test asserting that triggering a cheating penalty (`-30 SP`) on a profile with `10 SP` results in a `Current_SP` of `-20` (Deep Deficit).
* **Recovery Test:** Assert that a profile with `-20 SP` must accumulate exactly `20 SP` of positive gains to reach `0 SP` (the point where conversion is re-enabled).
* **Conversion Cap Test:** Assert that if `Current_SP` hits `30` but `Current_Shield_Count` is at `Max_Shield_Cap`, the conversion is blocked and `Current_SP` remains at `30`.
* **Atomic Consistency Test:** Verify that if the Shield conversion fails (due to database constraint), the SP deduction is also rolled back.