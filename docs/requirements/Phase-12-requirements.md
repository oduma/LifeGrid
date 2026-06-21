# LifeGrid - Phase 12 Vertical Slice Requirements
## Goal Card Actions: The "Overwhelmed" Protocol

This document specifies the structural, economical, and technical requirements for Phase 12. The objective is to implement the "Overwhelmed" interaction flow for active goals, allowing the user to either definitively abandon the goal or request an AI-driven schedule recalculation, with strict economic penalties applied.

---

## 1. External Reference Mapping
Claude Code must parse structural rules, visual layouts, and prompt templates directly from the master repository definitions:
* **Functional Logic:** `functional-requirements.md` (Section 4.2.1)
* **AI Prompt Templates (Recalculation):** `docs\specs\assets\prompts\prompt2.1.txt` and `docs\specs\assets\prompts\prompt2.2.txt`
* **Visual & Layout Tokens:**
  * Option A (Abandon): `docs\specs\assets\wireframes\wf12.png`
  * Option B (Extend/Recalculate): `docs\specs\assets\wireframes\wf13.png`

---

## 2. Domain & Application Layer (Action Handlers)

### 2.1 Option A: Abandon Goal Handler
When the user confirms the "Abandon Goal" action, the handler must execute the following state mutations in a single atomic database transaction:
1. **Status Mutation:** Update the target `Goal.Status` to `"Abandoned"`.
2. **Future Pruning:** Identify and permanently delete (or mark inactive) all future, incomplete `WeekGoal_Item`s and associated habits linked to this specific `GoalID`.
3. **Economic Penalty Calculation:** 
   * Sum the total XP the user has *historically earned* exclusively through this specific goal up to the current date.
   * Apply the penalty formula: `Total Penalty = Historical_Earned_XP + 100`.
4. **Economy Execution:** Deduct the `Total Penalty` from `UserProfile.Economy.Lifetime_XP`.

### 2.2 Option B: Recalculate Schedule (Extend Deadline) Handler
When the user submits an "Overwhelmed" text comment and requests a recalculation, the handler must execute the following:
1. **Temporal Math Extension:** 
   * Calculate the remaining `Duration` of the goal.
   * Mathematically extend both the total `Duration` and the exact `Deadline_Date` by exactly **+25%**.
2. **AI Orchestration (Prompt Pipeline):**
   * Package the updated goal parameters (new Deadline/Duration) and the user's text comment.
   * **Stage 1:** Pass the payload through `prompt2.1.txt` to generate the revised structural approach.
   * **Stage 2:** Pass the resulting output through `prompt2.2.txt` to generate the new, spread-out timeline of `WeekGoal_Item`s.
3. **Timeline Injection:** Overwrite the remaining unstarted weeks in the database with the newly generated, lower-density schedule from the AI.
4. **Economic Penalty:** Apply a strict flat penalty of **-100 XP** to `UserProfile.Economy.Lifetime_XP`.

---

## 3. Presentation Layer (UI Workflows & Gestures)

### 3.1 Goal Card Gesture Mappings
The Active Goal Card must implement horizontal swipe detection to trigger the corresponding resolution flows:
* **Swipe Left (Sweep Left):** Triggers the **Option A: Abandon Goal** warning flow. Visually reveals a destructive action background (e.g., Red) behind the card during the swipe.
* **Swipe Right (Sweep Right):** Triggers the **Option B: Recalculate Schedule** modal. Visually reveals a warning or recalculation background behind the card during the swipe.

### 3.2 Option A Layout (`wf12.png`)
* **Confirmation Warning:** Clearly display the calculated XP loss (e.g., `"Warning: You will lose [X] XP (All XP earned from this goal + 100 XP penalty)."`).
* **Action:** Require a definitive, destructive button press (e.g., long-press or red primary button) to finalize the abandonment following the left swipe.

### 3.3 Option B Layout (`wf13.png`)
* **Context Input:** Triggered by a right swipe, render a multi-line text area prompting the user to explain *why* they are overwhelmed. This string is critical for the AI recalculation.
* **Extension Warning:** Display the penalty warning: `"Extending the deadline by 25% will cost 100 XP."`
* **Loading State:** Upon confirmation, display the system's loading animation while the application contacts the Gemini API and processes the two-stage prompt pipeline.

---

## 4. Test-Driven Development (TDD) Invariants

* **Abandonment Math Verification:** Write a unit test that mocks a goal with exactly `450 XP` earned so far. Assert that triggering Option A deducts exactly `550 XP` from the user's profile.
* **Extension Math Verification:** Write a unit test that mocks a goal with a `100-day` duration. Assert that triggering Option B shifts the `Deadline_Date` mathematically by exactly `+25 days`.
* **Penalty Isolation Verification:** Ensure the `-100 XP` penalty from Option B does not revoke previously earned XP, but acts strictly as a flat reduction against the global `Lifetime_XP`.
* **Gesture Command Binding:** Verify that the UI layer correctly routes the `SwipeLeftCommand` and `SwipeRightCommand` on the Goal Card to their respective UI modal triggers without executing the database actions prematurely.