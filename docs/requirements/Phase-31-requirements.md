# LifeGrid - Phase 31 Vertical Slice Requirements
## Procrastination & Underachievement Engine (Weekly Escalator)

This document specifies the rigorous mathematical and state-transition logic for Phase 31. The objective is to implement the "Procrastination & Underachievement Escalator," integrating the 3-week compounding penalty system directly into the `CloseWeekCommand` established in Phase 30, strictly targeting individual `Goal` performance metrics.

---

## 1. External Reference Mapping
Claude Code must parse logic rules and state constraints directly from the master repository definitions:
* **Functional Logic:** `docs\specs\functional-requirements.md` (Section 5.1.1 - The Procrastination & Under-Achievement Escalator).
* **Data Schema:** `docs\specs\data-structure.json` (`Week_Goal_Item.Penalty_State`, `Week_Goal_Item.Metrics`, `Goal.Status`).
* **Design System Baseline:** `docs\specs\style-guide.md` (Visual warning tokens).

---

## 2. Domain & Application Layer (Calculation Engine)

### 2.1 Escalation Evaluation Service
Implement a dedicated domain service (e.g., `ProcrastinationEscalatorService`) that executes during the `CloseWeekCommand` process. This service evaluates the performance of *every individual goal* active in that week based strictly on its calculated `Goal_Weekly_GP`.

**Evaluation Logic (Per Goal):**

1.  **State Assessment & Transition:** Analyze the `Goal_Weekly_GP` against the goal's *current* `Penalty_State` inherited from the previous week.

    * **If Current State == Clean (Week 1 Trigger):**
        * If `Goal_Weekly_GP` <= 80%: Transition the goal's state to `Level_1_Warning`.
        * If > 80%: State remains `Clean`.

    * **If Current State == Level_1_Warning (Week 2 Squeeze):**
        * If `Goal_Weekly_GP` == 100%: Transition the goal's state back to `Clean`.
        * If `Goal_Weekly_GP` < 100%:
            * **Penalty Execution:** Immediately divide all `Goal_Weekly_XP_Earned` for that specific goal in that specific week by 2 (50% reduction). (Ensure rounding logic is sound, e.g., Math.Floor).
            * Transition the goal's state to `Probation_Week_2`.

    * **If Current State == Probation_Week_2 (Week 3 Reckoning):**
        * If `Goal_Weekly_GP` == 100%: Transition the goal's state back to `Clean`.
        * If `Goal_Weekly_GP` < 100%:
            * **Penalty Execution:** Immediately reduce `Goal_Weekly_XP_Earned` for that specific goal to **0**.
            * **System Lockout Trigger:** Update the parent `Goal.Status` to `Overwhelmed`. Set a global application state flag `IsSystemReckoningLockdown = true`.
            * Transition the goal's state to `Reckoning_Week_3`.

### 2.2 Atomic Command Integration
* The `CloseWeekCommand` (from Phase 30) must be updated to pass the target `Week` entity to the `ProcrastinationEscalatorService` *before* marking the week as `"Closed"`.
* All calculated XP penalties, state transitions, and potential global lockdowns must be persisted simultaneously via an atomic SQLite transaction.

---

## 3. Presentation Layer (MAUI UI Injection)

### 3.1 Warning Visuals & Shield Mitigation
* **Target View:** `WeeklyHabitsView` (and the `WeekSummaryView` representation).
* **Visual Warning Flag:** If a goal group (Zone B) evaluates to a `Penalty_State` greater than `Clean` (i.e., Level 1 Warning, Probation, Reckoning), render a prominent warning flag adjacent to the `Goal.Description`. Use the `Error` color token (`#FFFF1B77`) from the `style-guide.md` and the Google Material Icon `warning`.
* **The "Fix with Shield" Action:**
    * If a goal's state transitions to `Level_1_Warning`, dynamically render a `"Fix with Shield"` action button within that goal's grouping block.
    * **Visibility Condition:** This button is ONLY visible if `UserProfile.Economy.Current_Shield_Count > 0`.
    * **Execution:** Tapping the button deducts 1 Shield from the user's inventory, reverts the goal's `Penalty_State` back to `Clean`, and removes the warning visuals.

### 3.2 System Reckoning Lockdown UI
* If the `CloseWeekCommand` evaluation triggers the `Overwhelmed` state (Week 3 Reckoning failure), the application must aggressively redirect the user.
* **Routing:** Intercept all standard navigation and force-route the user directly to the specific `GoalView` for the Overwhelmed goal.
* **Interaction Block:** Prevent the user from navigating away to the Timeline, Home, or any other view until the `Overwhelmed` resolution process (defined in Phase 12: Abandon or Recalculate) is completed.

---

## 4. Test-Driven Development (TDD) Invariants

* **Week 1 Warning Assertion:** Write a unit test providing a `Clean` goal that finishes the week with 79% GP. Assert that the resulting state evaluates to `Level_1_Warning`.
* **Week 2 Penalty Math:** Write a unit test providing a goal entering a week in `Level_1_Warning` status. Provide habit logs resulting in 95% GP and generating 100 XP. Assert that the state shifts to `Probation_Week_2` and the final `Goal_Weekly_XP_Earned` strictly equals `50`.
* **Week 3 Lockdown Trigger:** Write a unit test providing a goal entering a week in `Probation_Week_2` status. Provide habit logs resulting in 99% GP. Assert that the `Goal_Weekly_XP_Earned` equals `0` and the parent `Goal.Status` mutates to `Overwhelmed`.
* **Shield Mitigation Logic:** Assert that executing the `"Fix with Shield"` command successfully decrements the inventory, resets the state to `Clean`, and prevents further escalation into Week 2.