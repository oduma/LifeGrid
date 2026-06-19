# LifeGrid - Phase 9 Vertical Slice Requirements
## The Hidden Vices Survey & Analysis Pipeline

This document defines the strict requirements for Phase 9, implementing the "Hidden Vices Survey" as detailed in `functional-requirements.md` (Sections 1.7 and 1.8). The objective is to build a globally accessible, single-use AI survey pipeline that dynamically generates behavioral questions, tracks user progress, and analyzes the responses to establish the user's permanent Vice Log.

---

## 1. External Reference Mapping
Claude Code must parse structural rules, prompt templates, and data schemas directly from the master repository definitions:
* **Functional Logic:** `docs\specs\functional-requirements.md` (Sections 1.7 & 1.8)
* **AI Prompt Templates:** `docs\specs\assets\prompts\prompt3.1.txt` (Question Generation) and `docs\specs\assets\prompts\prompt3.2.txt` (Answer Analysis)
* **Visual & Layout Tokens:** `docs\specs\style-guide.md` and `docs\specs\screen-layout-specification.md`
* **Data Schema:** `docs\specs\data-structure.json` (Specifically the `UserProfile` Vice attributes)

---

## 2. Domain & Application Layer (State Architecture)

### 2.1 Single-Use Lifecycle Constraint
* **State Flag:** Introduce a boolean invariant to the `UserProfile` aggregate (e.g., `IsViceSurveyCompleted`).
* **Global Access Command:** Create an application-level routing command (e.g., `LaunchViceSurveyCommand`) that can be bound to buttons across the application (like the one created in the Phase 7 User Setup Hub).
* **Execution Gate:** When `LaunchViceSurveyCommand` is invoked, the application layer must query `UserProfile.IsViceSurveyCompleted`. 
  * If `true`: The command safely aborts or displays a non-intrusive toast notification: `"Hidden Vices already established. Factory Reset required to retake."`
  * If `false`: Route the user into the active Survey View.

---

## 3. Infrastructure & AI Integration Layer (Gemini Pipeline)

### 3.1 Chained AI Execution Pipeline
Extend the internal `GeminiOrchestrator` to handle a two-stage sequential workflow:
* **Execution Loop 1 (Question Generation):** * Load `docs\specs\assets\prompts\prompt3.1.txt`. Inject the user's current active `Goal` context to generate highly tailored, contextual questions about their specific habits and potential pitfalls.
  * Parse the JSON response into a structural array of `SurveyQuestion` objects.
* **Execution Loop 2 (Vice Extraction & Analysis):**
  * Upon survey completion, package the user's free-text answers. Load `docs\specs\assets\prompts\prompt3.2.txt`, inject the Q&A payload, and forward to Gemini.
  * Parse the returned JSON into concrete `Vice` entities (including triggers, warning signs, and category mappings).

### 3.2 Database Persistence
* **Atomic Transaction:** Deserialize the output from Loop 2. Open an explicit EF Core/SQLite database transaction to save the newly identified `Vice` entities linked to the `UserProfile`.
* Upon a successful write, permanently flip the `UserProfile.IsViceSurveyCompleted` flag to `true` and commit the transaction.

---

## 4. Presentation Layer (MAUI UI Workflow Injection)

### 4.1 Survey View Layout & Progress Tracker
Within the flexible Main Interaction Area, construct the survey interface:
* **Top Anchor (Progress Tracker):** Render a dynamic text tracker utilizing the secondary typography token (e.g., `"Question {CurrentIndex} of {TotalCount}"`). Beneath the text, render a horizontal linear progress bar utilizing the `Primary` accent color.
* **Center Flex Container (Active Question):**
  * Display the active question string utilizing the primary typography token.
  * Render a multi-line text input canvas (`2px` corner radius) mapped to the `On-Surface` text color.
* **Bottom Action Controller:**
  * Render a button labeled `"Next"` (or `"Analyze Profile"` on the final question).
  * Clicking the button caches the active answer and animates the transition (bottom-up slide as defined in `docs\specs\style-guide.md`) to the next question.

### 4.2 Loading & Finalization States
* **Pre-Survey Loading:** Display the typewriter animation state while Execution Loop 1 contacts Gemini to fetch the dynamic questions.
* **Post-Survey Processing:** Display the loading state while Execution Loop 2 analyzes the answers.
* **Completion Screen:** Render a brief summary of the detected Vices, followed by an `"Accept & Return"` button that drops the survey view from the navigation stack and returns the user to their originating hub.

---

## 5. Test-Driven Development (TDD) Invariants

* **Lifecycle Gate Verification:** Write an application-layer unit test asserting that invoking the `LaunchViceSurveyCommand` when `IsViceSurveyCompleted == true` throws an expected domain exception or returns a bypass result without opening the view model.
* **AI Orchestration Mocking:** Write isolated unit tests simulating the full two-prompt chain. Assert that the string answers provided to mock Loop 1 correctly build the JSON payload injected into mock Loop 2.
* **Progress Math Verification:** Assert that the ViewModel correctly calculates the integer index for the Progress Tracker, ensuring it mathematically reaches `100%` explicitly on the final question.