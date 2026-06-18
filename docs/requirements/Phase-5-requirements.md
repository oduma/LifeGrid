# LifeGrid - Phase 5 Vertical Slice Requirements
## Onboarding Step 1 AI Validation & Goal Refinement Pipeline

This document specifies the functional and technical requirements for Phase 5. This vertical slice implements the user setup sequence corresponding to sections 1.1, 1.2, 1.3, and 1.4 of `functional-requirements.md`. It leverages Gemini AI pro to validate the user's initial goal draft and gather refinement parameters, concluding with persisting records into the production storage aggregates.

This is kicked off by the user clicking next on the seting of the goal screen. and will get as input the goal as free text entered by the user.
---

## 1. Project Reference Directives
Claude Code must dynamically load contexts from project system files. Do not copy or duplicate text templates or mapping keys within source code:
* Onboarding Flow Matrix & Logic: `docs\specs\functional-requirements.md` (Steps 1.1, 1.2, 1.3, 1.4)
* Goal Validation System Prompt: `docs\specs\assets\prompts\prompt1.txt`
* Deep Refinement Question Prompt: `docs\specs\assets\prompts\prompt2.txt`
* Core Entity Schemas & Mappings: `docs\specs\data-structure.json` (Phase 4 Database Tables)
* Layout Framework & Visual Styling: `docs\specs\screen-layout-specification.md` and `docs\specs\design-system.md`

---

## 2. Domain & Application Layer Architecture (Strict DDD & TDD)

### 2.1 Onboarding Session State Progression
* Expand the `OnboardingSession` domain entity sequence boundaries:
  * `Step1_GoalDraftCaptured` (Phase 2 Baseline State)
  * `Step1_AwaitingValidation` (Awaiting Gemini validation analysis)
  * `Step1_RefinementQuestionsActive` (Validation passed; additional clarity questions active)
  * `Step1_ExecutionVerified` (Explicit action confirmed; entries stored)

### 2.2 Domain Business & Boundary Constraints
* **Scope Constraint:** This phase must stop *short* of generating habits. No production `Habit` entities or tracking schedules may be created or committed.
* **Encapsulation Protection:** Form entries, AI clarifications, and validated parameters must be parsed out of external JSON payloads into validated value objects before instantiating or hydrating the production domain entities (`UserProfile` and `Goal`).

---

## 3. Infrastructure & AI Integration Layer (Gemini & SQLite)

### 3.1 Secure AI Client Service
* Create an internal query component: `GeminiOnboardingOrchestrator`.
* **Credential Retreival:** Fetch the required API key from the local OS key registry via `SecureStorage.Default.GetAsync("Gemini_Provider_Token")`.
* **Execution Engine Loop:**
  * **Loop 1 (Validation):** Load the template from `docs\specs\assets\prompts\prompt1.txt`. Combine it with the stored text from the database cache (`OnboardingProgressCache.RawGoalDraft`) and forward it to Gemini. Parse the returned content.
  * **Loop 2 (Refinement):** Load the text pattern from `docs\specs\assets\prompts\prompt2.txt`. Use it to prompt Gemini to output tailored clarity questions based on the structural gaps identified in the user's entry.

### 3.2 Production Database Commit Boundaries
* Upon finalization of this sequence, transform the consolidated cached payload into structural models mapping directly into:
  * `UserProfile` (Initializing initial structural stats, level properties, and default values).
  * `Goal` (Building out the root validated goal entry with names, parameters, and metadata).
* **Explicit Transaction Gate:** Writes to production tables must occur only when the user explicitly triggers the ultimate action control on the refinement form.
* **Answers to the refinement questions:** Should be written in their own data structure linked with the Goal the structure should probably be a key value pair with the key being the question and the value being the answer of the user.
* **Staging Cleanup Mandate:** As the onboarding process is not yet finished the Onboarding cache shoudl keep track of the progress of the user.

---

## 4. Presentation Layer (MAUI UI Workflow Injection)

### 4.1 Global Guard Constraints
* Bottom navigation items remain disabled (`IsEnabled="False"`). HUD display modules remain in a transparent, uninitialized state.

### 4.2 Interactive Center View Execution Layout
* **Visual State: Awaiting Validation:** Swap out the Phase 2 completed block for an active asynchronous waiting state. Render a subtle, center-aligned loading view utilizing the typewriter transition animation token defined in `docs\specs\style-guide.md`.
* **Visual State: Clarification Prompting:** Dynamically render the response questions parsed via `docs\specs\assets\prompts\prompt2.txt` inside the central interaction area. 
  * Apply secondary monospace typography tokens to all questions.
  * Provide corner-constrained text inputs (2px radius) matching the generated query layout.
* **Final Form Submittal:** Render a command controller labeled `"Confirm & Initialize"`. Clicking this button locks inputs, opens the infrastructure mapping context, commits data to production SQLite tables, cleans up the onboarding progress cache, and updates the center area text block to: `"Goal Refined and Stored. Ready for Phase 6 Habit Generation."`

---

## 5. Test-Driven Development (TDD) Invariants

* **AI Service Layer Mocking:** Write comprehensive `xUnit` tests using isolation patterns to intercept and substitute external Gemini API network endpoints.
* **Strict Verification Asserts:**
  * Assert that the system accurately handles unexpected or malformed AI JSON structures without crashing or corrupting runtime memory.
  * Assert that no data mutations occur in production tables if the refinement flow is exited or abandoned prior to the explicit click of the submittal controller.
  * Assert that the staging cache record is completely eradicated post successful execution transaction.