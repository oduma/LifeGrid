# LifeGrid - Phase 27 Vertical Slice Requirements
## "I Want More": AI-Generated Moment Burst Habits

This document defines the strict requirements for Phase 27. The objective is to implement the "I Want More" functionality, allowing the user to request spontaneous, short-term habits ("Moment Burst") that the AI generates and injects directly into the current week's goal structure.

---

## 1. External Reference Mapping
Claude Code must parse structural rules, prompt templates, and data schemas directly from the master repository definitions:
* **Functional Logic:** `functional-requirements.md` (Section 4.3.1).
* **AI Prompt Template:** `Prompt5.txt`.
* **Data Schema:** `data-structure.json` (`Habit`, `Week_Goal_Item`).
* **Visual Tokens:** `design-system.md` (Colors/Typography), `style-guide.md` (Iconography/Visual hierarchy).

---

## 2. Domain & Application Layer (Action Logic)

### 2.1 Prompt Orchestration Pipeline
* Implement the `RequestMomentBurstCommand` handler.
* **Context Injection:** When the user triggers the action, the service must collect the current `Goal.Description` and the current `Week` temporal context.
* **Execution:**
  * Forward the context + User Request string to Gemini using the structure defined in `Prompt5.txt`.
  * **Parser Directive:** The AI response must return a structured JSON response indicating the habit details or an explicit "Denial" status if the request is deemed invalid or outside scope.

### 2.2 Domain State Mutation (Acceptance vs. Denial)
* **If Denial:** The system must gracefully display a non-intrusive UI message (e.g., "AI suggests staying the course for now"). Do not alter the database.
* **If Acceptance:**
  * Instantiate a new `Habit` entity.
  * **Marking Constraint:** Set `Habit_Type` strictly to `"Moment Burst"`.
  * **Linkage:** Insert the habit directly into the current `Week_Goal_Item.Habits` collection.
  * **Persistence:** Commit the new habit to SQLite via an atomic database transaction.

---

## 3. Presentation Layer (UI & Visual Hierarchy)

### 3.1 UI Trigger
* **Placement:** Within the `WeeklyHabitsView` (and Home Dashboard), render an action button labeled `"I Want More"`.
* **Trigger:** Tapping this button opens an input modal prompting the user to describe what they are looking for (e.g., "I have 10 minutes to focus, give me something useful").

### 3.2 Visual Identification of "Moment Burst"
* To ensure "Moment Burst" habits are visually distinct, the Habit Card must render a unique visual treatment:
  * **Iconography:** Use a specific Google Material Symbol (e.g., `bolt` or `electric_bolt`) to signify the burst nature of the habit.
  * **Color Token:** Use the `Primary` neon accent color (`#35f8db`) for the border of the Habit Card.
  * **Label:** Explicitly include a "Moment Burst" badge or text element using the secondary typography token (Share Tech Mono) at the top of the habit card.

---

## 4. Test-Driven Development (TDD) Invariants

* **AI Logic Verification:** Write a unit test simulating the AI interaction. Assert that when the AI returns a "Denial" status, no database record is created, and the `GetWeeklyHabitsQuery` output remains unchanged.
* **Linkage Persistence Test:** Assert that a generated "Moment Burst" habit correctly sets its `GoalID` and `WeekID` foreign keys, ensuring it appears correctly in the correct dashboard group.
* **Visibility Assert:** Write a UI test asserting that any `Habit` with the property `Habit_Type == "Moment Burst"` renders with the designated neon border color and the specific material icon (bolt), distinguishing it from standard `Planned` habits.