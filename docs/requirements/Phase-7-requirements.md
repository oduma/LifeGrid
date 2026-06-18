# LifeGrid - Phase 7 Vertical Slice Requirements
## User Setup Hub Evolution & Route Corrections

This document specifies the strict requirements for Phase 7. The objective is to decouple the initial onboarding sequence from the persistent user profile view, rename the initial view, and construct the permanent "User Setup" control center accessed via the global HUD.

---

## 1. External Reference Mapping
Claude Code must parse visual and layout bounds directly from the master repository definitions. Do not hardcode raw hex values or padding metrics:
* Global Layout & Spacing: `docs\specs\screen-layout-specification.md`
* Styling Tokens & Shapes (2px radius, monospace fonts): `docs\specs\style-guide.md`
* Routing & Menu Definitions: `docs\specs\navigation-architecture.md`

---

## 2. Refactoring & Route Correction

### 2.1 Onboarding View Renaming
* Rename the active Phase 2/Phase 5 onboarding sequence UI component from "User Setup" (or its current placeholder name) to `"Create Goal"`. 
* Ensure the route and view models clearly reflect that this is a specific domain action (creating a goal) rather than the global user configuration space.
* If the user has at least one Goal already established the app should start with the main menu enabled and it should automatically open the Goals page 

---

## 3. Domain & Application Layer Architecture

### 3.1 State Assessment Queries
* Implement a lightweight query handler to check the count of active `Goal` entities in the SQLite database. This boolean result (`HasActiveGoals`) will drive the UI warning state.

### 3.2 Destructive Command Handlers
* **Factory Reset Command:** Implement an atomic database execution command that drops and recreates, or fully truncates, all production tables (`UserProfile`, `Goal`, `Week`, `WeekGoal`, `Habit`) and resets the `OnboardingSession` cache to `Unstarted`.

---

## 4. Presentation Layer (The User Setup Hub)

### 4.1 HUD Routing Hook
* Update the Global HUD Panel (Top View). The left action anchor (`account_circle` icon) must now drop its `no-op` state and route the user to the new `UserSetupView`.

### 4.2 Main Interaction Area: User Setup View
* Render the User Setup control center within the flexible center viewport.
* **Component 1: Goal Management Block**
  * Render a secondary-styled button: `"Edit Active Goals"`. Tapping this routes the user to the primary Goals list view.
  * Render a primary/destructive button: `"Reset Goals"`.
* **Component 2: Conditional Warning State**
  * Bind to the `HasActiveGoals` query.
  * **If True:** Display a highly visible text warning block (utilizing `Error` or accent color tokens from `docs\specs\style-guide.md`). 
  * **Literal Text:** `"Warning: Resetting your goals will permanently wipe all current goals, Shield Points, XP, and restart your game from scratch."`
* **Component 3: Diagnostics & Progression**
  * Render a standard action button: `"Detect Hidden Vices"`. 
  * **Interaction Law:** Bound to a safe `no-op` state for this phase. Navigation for the Vice Survey will be defined in a future slice.

---

## 5. Test-Driven Development (TDD) Invariants

* **Conditional Rendering Verification:** Write a UI/ViewModel unit test asserting that the warning message string is structurally hidden/null when the mocked `Goal` count is `0`, and explicitly visible when `> 0`.
* **Atomic Wipe Verification:** Write an integration test executing the Reset Goals command against an in-memory SQLite database populated with dummy data. Assert that exactly zero records remain across all domain tables post-execution.