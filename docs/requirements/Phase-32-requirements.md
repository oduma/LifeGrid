# LifeGrid - Phase 32 Vertical Slice Requirements
## Flash Quests & Temporal Multipliers

This document specifies the rigorous logic for Phase 32. The objective is to implement the "Flash Quest" mechanic, a background-triggered AI intervention designed to rescue lagging goals via a time-sensitive, high-reward rescue task, resulting in a global "Double XP" state.

---

## 1. External Reference Mapping
Claude Code must parse structural rules and logic chains from the following definitions:
* **Functional Logic:** `docs\specs\functional-requirements.md` (Section 3.7 - Flash Quests, Section 4.4.1 - Rescue Mechanics).
* **AI Integration:** `docs\specs\assets\prompts\Prompt8.txt`.
* **Data Schema:** `docs\specs\data-structure.json` (`Habit.Habit_Type` set to `"Flash"`, `Week.Global_Metrics`).

---

## 2. Domain & Application Layer (Flash Quest Engine)

### 2.1 The Temporal Trigger (Background Service)
* Implement a background worker (or extension to the Phase 30 worker) that fires **strictly on Thursdays at 12:00 PM local time**.
* **Pre-Condition Evaluation:** The worker must query the current active `Week` and iterate through all associated `Week_Goal_Items`.
* **Threshold Check:** For each goal, calculate the current completion ratio. If *any* goal currently sits at **< 50% completion** against its weekly target, the Flash Quest pipeline is activated. If all goals are >= 50%, execution halts.

### 2.2 The AI Orchestration Pipeline
* If the threshold check fails, instantiate the `GenerateFlashQuestCommand`.
* **Payload Construction:** Package the lagging goals and their context as defined by the inputs required in `Prompt8.txt`.
* **AI Execution:** Submit the payload to the Gemini API.
* **Response Parsing:**
  * If the AI returns strictly `"N/A"`, the engine halts execution gracefully.
  * If the AI returns a valid JSON array (`flash-quests`), proceed to the Domain Injection phase.

### 2.3 Domain Injection & Deadlining
* For each object returned in the `flash-quests` array:
  * Instantiate a new `Habit` entity.
  * **Strict Typing:** Set `Habit_Type` to `"Flash"`.
  * **Absolute Deadline:** Calculate and assign a strict `Target.Deadline_DateTime` exactly **24 hours** from the moment of injection (Friday 12:00 PM local).
  * Persist these new habits to the active `Week_Goal_Items`.

---

## 3. Economy Layer (Double XP State)

### 3.1 The Multiplier Activation
* Extend the `LogHabitProgressCommand` (Phase 22) and the `GamificationCalculationEngine` (Phase 23) to recognize the `"Flash"` habit type.
* **Trigger:** If a user successfully completes a habit where `Habit_Type == "Flash"` *before* its 24-hour deadline expires, the system must trigger a global state mutation.
* **State Mutation:** Update the current `Week` aggregate to activate a boolean flag: `IsDoubleXpActive = true`.

### 3.2 Multiplier Math
* While `IsDoubleXpActive == true` for the current week, the `GamificationCalculationEngine` must intercept all subsequent XP awards (for *any* habit type, including the Flash task itself) and mathematically apply a `x2` multiplier before persisting to the user's `Lifetime_XP`.

---

## 4. Presentation Layer (MAUI UI Injection)

### 4.1 Flash Task Visualization
* Within the `WeeklyHabitsView` and `HomeView` dashboards, Flash habits must be violently visually distinct.
* **Styling Tokens:** Apply a pulsating or high-contrast border (e.g., using the `Secondary` token `#e5cde1` or a custom Neon effect).
* **Iconography:** Use a high-urgency Google Material Symbol (e.g., `local_fire_department` or `timer`).
* **Countdown:** Render a highly visible countdown timer (`"Expires in Xh Ym"`) directly on the card.

### 4.2 Global "Double XP" Indicator
* If the user successfully completes the Flash Quest and the `IsDoubleXpActive` flag switches to true, the UI must reflect this globally.
* **HUD Injection:** Render a permanent, glowing `"2x XP ACTIVE"` badge in the Phase 8 Global HUD for the remainder of the week.
* **Dashboard Feedback:** Any habit cards completed while the multiplier is active should display their earned points explicitly showing the math (e.g., `+50 XP (x2)`).

---

## 5. Test-Driven Development (TDD) Invariants

* **Temporal Execution Test:** Mock the system clock to Thursday 12:01 PM. Provide a mocked data state where one goal is at `45%`. Assert that the `GenerateFlashQuestCommand` is triggered.
* **Double XP Activation Test:** Write a unit test simulating the completion of a `"Flash"` habit within the 24-hour window. Assert that the `Week.IsDoubleXpActive` state flips to `true`.
* **Multiplier Math Assertion:** With `IsDoubleXpActive == true`, pass a standard `Planned` habit completion worth 20 base XP through the `GamificationCalculationEngine`. Assert that exactly 40 XP is added to the user's profile.