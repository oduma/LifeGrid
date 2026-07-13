# LifeGrid - Phase 33 Vertical Slice Requirements
## Vice Check Audit ("Test me I'm being good") & Penalties

This document defines the strict structural and technical requirements for Phase 33. The objective is to implement the reactive "Vice Check" audit loop from the Week Summary screen, orchestrating a two-step AI prompt pipeline (`Prompt6.txt` and `Prompt7.txt`) to evaluate hidden habit compliance and apply retroactive mathematical penalties.

---

## 1. External Reference Mapping
Claude Code must parse structural rules and logic chains from the following definitions:
* **Functional Logic:** `docs\specs\functional-requirements.md` (Section 5.2 - The "Test me I'm being good" Action).
* **AI Integration:** `docs\specs\assets\prompts\Prompt6.txt` (Question Generation) and `docs\specs\assets\prompts\Prompt7.txt` (Answer Validation).
* **Data Schema:** `docs\specs\data-structure.json` (`UserProfile.IsViceSurveyCompleted`, `Goal` bad habit linkages, `Week_Goal_Item.Metrics.Goal_Weekly_GP`).

---

## 2. Domain & Application Layer (Audit Engine)

### 2.1 Trigger & Availability Rules
* **Temporal Window:** The action is strictly valid for **72 hours** immediately following the `Week.End_Date` (i.e., Sunday 11:59 PM to Wednesday 11:59 PM).
* **Prerequisite:** `UserProfile.IsViceSurveyCompleted` MUST evaluate to `true`.
* **State Check:** The target `Week` must be in a `"Closed"` state.

### 2.2 Stage 1: Initiation & Question Generation
Implement the `InitiateViceCheckCommand`:
1. **Instant Reward:** Immediately award a flat **+20 XP** to the user's `Lifetime_XP` for triggering the audit, regardless of the outcome. Persist this economy change.
2. **Context Selection:** Identify all `Goal` entities active during that specific week. From those goals, randomly select exactly one associated `Linked Bad Habit` (which contains a `Danger_Level` and a `Description`).
3. **AI Generation:** Pass the selected bad habit context into `Prompt6.txt` and call the Gemini API. 
4. **Return:** The API will return a single, subtle, targeted string question. Return this string to the UI.

### 2.3 Stage 2: Resolution & Penalty Math
Implement the `ResolveViceCheckCommand`:
1. **AI Validation:** Package the original question, the bad habit context, and the user's submitted string answer. Pass this payload into `Prompt7.txt` and call the Gemini API.
2. **Parser Directive:** Parse the resulting JSON to extract the `persists` boolean.
3. **Outcome Execution:**
   * **If `persists == false` (User passed):** Do nothing mathematically. Return a success state.
   * **If `persists == true` (User failed):** * Identify the `Danger_Level` (Integer 1-10) of the target bad habit.
     * Calculate Penalty: `Penalty % = Danger_Level * 1%`.
     * Identify the specific `Week_Goal_Item` associated with the habit.
     * Retroactively subtract the `Penalty %` from `Metrics.Goal_Weekly_GP`.
     * **Constraint:** If the GP drops <= 80% due to this retroactive penalty, it MUST instantly trigger the Procrastination Escalator (Phase 31) logic, shifting the goal to `Level_1_Warning`.
4. **Atomic Commit:** Save the mutated `Goal_Weekly_GP` and any subsequent penalty state shifts to SQLite in a single transaction.

---

## 3. Presentation Layer (MAUI UI Injection)

### 3.1 Week Summary View Updates
* **Action Button:** Render a secondary action button labeled `"Test me I'm being good"`.
* **Visibility Binding:** This button MUST disappear automatically if the local device time exceeds `Week.End_Date + 72 hours` OR if `IsViceSurveyCompleted == false`.
* **Once-Off Execution:** Once the audit is completed for a specific week, disable or hide the button to prevent multiple spam audits on the same week.

### 3.2 The Audit Modal
* **Question View:** When Stage 1 completes, render a modal/bottom sheet displaying the AI-generated question using the primary typography token.
* **Input:** Provide a multi-line text area for the user to submit their justification/answer.
* **Loading State:** Show a prominent loading indicator while Stage 2 contacts Gemini to evaluate the answer.

### 3.3 Visual Feedback
* **Success:** Display a high-fidelity success toast/message (e.g., "Integrity maintained. 20 XP secured.").
* **Failure:** Display a prominent Error-colored notification detailing the penalty (e.g., "Vice detected. -X% GP applied retroactively."). Asynchronously refresh the parent `WeekSummaryView` so the user instantly sees the drop in their GP progress bar.

---

## 4. Test-Driven Development (TDD) Invariants

* **Temporal Visibility Test:** Write a view model test asserting that if the current mocked time is `Week.End_Date + 73 hours`, the `IsViceCheckVisible` property rigidly evaluates to `false`.
* **Penalty Math Test:** Write a unit test providing a bad habit with a `Danger_Level` of `5`, a starting GP of `82%`, and a mocked AI response of `persists: true`. Assert that the resulting GP is exactly `77%` AND that the `Penalty_State` correctly shifts from `Clean` to `Level_1_Warning`.
* **Instant XP Test:** Ensure the `+20 XP` is awarded in Stage 1 independently of Stage 2's outcome.