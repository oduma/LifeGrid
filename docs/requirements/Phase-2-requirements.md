# LifeGrid - Phase 2 Vertical Slice Requirements
## Onboarding State Engine & Step 1 Setup (Goal Draft)

This document outlines the functional and technical requirements for Phase 2. The goal is to build an isolated, persistent onboarding state machine and implement the very first step of user setup (capturing a raw goal draft) as a strict vertical slice.

---

## 1. Architectural & File References
Claude Code must pull design configurations from existing tracking documentation. Do not duplicate concrete numbers inside implementation files:
* Layout Framework & Boundaries: `docs\specs\screen-layout-specifications.md`
* UI Color/Typography Tokens: `docs\specs\style-guide.md`
* Master Navigation Definitions: `docs\specs\navigation-architecture.md`

---

## 2. Domain & Application Layer (TDD Enforced)

### 2.1 Onboarding State Machine
* Create an `OnboardingSession` domain tracking component.
* **State Enumeration:** Define clear sequence phases:
  * `Unstarted`
  * `Step1_GoalDraftCaptured`
* **Invariants & Rules:**
  * The session must explicitly flag whether the full initialization sequence is finalized (`IsComplete`).
  * For Phase 2, `IsComplete` will remain permanently `false` because subsequent setup steps do not exist yet.

### 2.2 Core Business Rules
* If `OnboardingSession.IsComplete` is false, global navigation access is blocked.
* The draft goal data captured during this step is a pure string entry and must bypass all structural domain validations (durations, categories, deadlines) required by the production `Goal` aggregate.

---

## 3. Infrastructure & Persistence Layer (SQLite)

### 3.1 Onboarding Cache Architecture
* Create a dedicated, lightweight cache table: `OnboardingProgressCache`. 
* **Strict Separation:** This table must remain entirely independent of the future production `Goals` and `UserProfile` tables.
* **Schema Fields:**
  * `SessionId` (Primary Key / Guid)
  * `CurrentStep` (Integer / String representing state)
  * `RawGoalDraft` (Nullable Text string)
  * `LastActiveTimestamp` (DateTime)

### 3.2 Lifecycle Interception Mechanics
* **App Launch Event:** On initialization, the app must query `OnboardingProgressCache`.
  * *Case A (No Record / Unstarted):* Initialize a new tracking record in the DB and route directly to Step 1.
  * *Case B (Record Found, `CurrentStep` == `Unstarted`):* Render the clean Step 1 entry screen.
  * *Case C (Record Found, `CurrentStep` == `Step1_GoalDraftCaptured`):* Load the saved string into the UI field and display a placeholder for the next phase's entry mechanism.

---

## 4. Presentation Layer (MAUI UI Shell Injection)

### 4.1 Global Guardrails & Navigation Locks
* **Bottom Menu:** Query the `OnboardingSession.IsComplete` state. Because it evaluates to `false`, force all bottom tabs fetched from `navigation-architecture.md` to a disabled visual and interactive state (`IsEnabled="False"`).
* **HUD Panel:** Remains in the visible, uninitialized placeholder state from Phase 1. Tapping HUD icons continues to yield `no-op`.

### 4.2 Main Interaction Area: Step 1 Setup View
* Replace the literal `"Placeholder Text"` from Phase 1 with the Step 1 input workflow:
  * **Input Field:** A text input area mapped to the secondary font token in `design-system.md`.
  * **Action Button:** A button labeled `"Next"` conforming to the 2px corner radius metric.
* **Behavioral Execution:**
  * As the user types, every text change or focus loss event should write safely to the `RawGoalDraft` cache field in the local SQLite database.
  * Tapping `"Next"` updates `CurrentStep` to `Step1_GoalDraftCaptured` in the DB and transitions the Main Interaction Area to show a temporary text block: `"Step 1 Complete. Awaiting Phase 3 setup steps."`