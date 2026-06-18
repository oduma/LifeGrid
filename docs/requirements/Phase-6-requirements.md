# LifeGrid - Phase 6 Vertical Slice Requirements
## AI Habit Generation Pipeline & Onboarding Finalization

This document specifies the strict requirements for Phase 6. The objective is to execute the final steps of the user onboarding sequence (section 1.5), utilize a chained two-prompt Gemini pipeline to generate habit structures, persist them to the database, and finally unlock the global application navigation.

---

## 1. External Reference Mapping
Claude Code must parse structural matrices, prompt templates, and layout states directly from the master repository definitions:
* Functional Flow: `docs\specs\functional-requirements.md` (Section 1.5)
* AI Prompt Templates: `docs\specs\assets\prompts\prompt2.1.txt` and `docs\specs\assets\prompts\prompt2.2.txt`
* Database Schemas: `docs\specs\data-structure.json`
* Viewport Layouts & Navigation: `docs\specs\screen-layout-specification.md` and `docs\specs\navigation-architecture.md`

---

## 2. Domain & Application Layer Architecture (DDD & TDD Enforced)

### 2.1 Entity Realization (Habit & Week Structures)
* Implement the pure C# domain entities in `LifeGrid.Domain`:
  * `Week` (Temporal tracking bounds)
  * `WeekGoal` (Sub-objectives linked to a week)
  * `Habit` (The core executable action)
* **Strict Encapsulation:** These entities must enforce validation invariants mathematically. They must remain fully decoupled from SQLite and Entity Framework Core mechanics.

### 2.2 Onboarding Sequence Finalization
* Update the `OnboardingSession` domain entity to support the terminal state:
  * `Step6_HabitsGenerated`
* Set the `IsComplete` global invariant flag to `true` upon successful execution of this state.

---

## 3. Infrastructure & AI Integration Layer (SQLite & Gemini)

### 3.1 Chained AI Execution Pipeline
* Expand the `GeminiOnboardingOrchestrator` to handle sequential, dependent API calls.
* **Execution Loop 1 (Blueprint Generation):** Load `docs\specs\assets\prompts\prompt2.1.txt`. Inject the validated `Goal` data from Phase 5 and forward to Gemini.
* **Execution Loop 2 (Habit Expansion):** Capture the explicit JSON output from Loop 1. Load `docs\specs\assets\prompts\prompt2.2.txt`, inject the Loop 1 output as context, and forward to Gemini to generate the final granular breakdown of Weeks, Week Goals, and Habits.

### 3.2 Database Commit & Schema Migration
* **EF Core Mappings:** Define Fluent API configurations in the Infrastructure layer mapping the `Week`, `WeekGoal`, and `Habit` entities to tables matching the `data-structure.json` specification. Generate and apply the required migration.
* **Atomic Transaction:** Deserialize the output of AI Loop 2. Open an explicit database transaction to write the generated records into the SQLite production tables. If any structural mapping fails, roll back the entire transaction.

---

## 4. Presentation Layer (MAUI UI Workflow Injection)

### 4.1 Global Navigation Unlock
* Query the `OnboardingSession.IsComplete` flag (which now evaluates to `true`).
* **Bottom Menu:** Remove the disabled override (`IsEnabled="False"`) on all bottom tabs defined in `docs\specs\navigation-architecture.md`, returning them to an active, interactive state.
* **HUD Panel:** Transition the HUD to its operational state, enabling the `User Setup` and `Notifications` action anchors.

### 4.2 Main Interaction Area & Routing
* **Loading State:** While the chained AI prompts execute, display a center-aligned processing indicator in the Main Interaction Area using standard design system tokens.
* **Transition & Redirection:** Upon a successful atomic database commit, immediately route the user away from the setup flow.
* **Target View (Goals List):** Replace the Main Interaction Area with the initial `Goals` list view:
  * Render a list container displaying the newly generated goal.
  * Render a primary `"Add Goal"` controller button (utilizing the 2px corner radius metric) enabling the user to start the process again.

---

## 5. Test-Driven Development (TDD) Invariants

* **Chained AI Mocking:** Write isolated unit tests simulating the sequential Gemini API calls. Assert that the output of mock Call 1 is properly injected into the payload of mock Call 2.
* **Database Rollback Verification:** Implement an integration test forcing a deserialization or structural mapping error on the response of Loop 2. Assert that the database transaction rolls back perfectly, leaving zero orphaned `Habit` or `Week` records in the SQLite context.