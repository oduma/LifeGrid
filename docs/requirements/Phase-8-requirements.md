# LifeGrid - Phase 8 Vertical Slice Requirements
## Global HUD Metric Wiring & Economy Data Binding

This document specifies the structural and technical requirements for Phase 8. The objective is to replace the empty HUD placeholder established in Phase 1 with a live, data-bound telemetry panel that aggregates the user's lifetime stats against their current weekly performance, heavily emphasizing immediate short-term progress.

---

## 1. External Reference Mapping
Claude Code must parse visual and layout bounds directly from the master repository definitions. Do not hardcode layout constraints or raw hex colors:
* Spatial Layout & HUD Height Budgets: `docs\specs\screen-layout-specification.md`
* Typography (Monospace digits) & Colors: `docs\specs\style-guide.md`
* Database Schema & Property Paths: `docs\specs\data-structure.json`

---

## 2. Domain & Application Layer (Query Architecture)

### 2.1 HUD Telemetry Aggregation Service
* Implement a dedicated query handler responsible for pulling and calculating the specific n1-n9 values.
* **Data Sources Required:** * `UserProfile` (For lifetime stats and current capacities).
  * `Week` (The currently active temporal week).
  * `WeekGoal` items associated with the active week.

### 2.2 Strict Data Mapping Formulas
The handler must extract and compute the following specific variables:
* **n1 (Level):** `UserProfile.Current_Level`
* **n2 (Lifetime GP):** `UserProfile.Economy.Lifetime_GP_Average`
* **n3 (Current Weekly GP):** The mathematical average of `Metrics.GoalWeekly_GP` across all active `WeekGoal_Item`s for the current week.
* **n4 (Lifetime XP):** `UserProfile.Economy.Lifetime_XP`
* **n5 (Weekly XP):** The sum total of `Metrics.Goal_Weekly_XP_Earned` across all active `WeekGoal_Item`s for the current week.
* **n6 (Current SP):** `UserProfile.Economy.Current_SP`
* **n7 (Weekly SP Earned):** `Week.Global_Metrics.Total_Weekly_Earned_SP`
* **n8 (Active Shields):** `UserProfile.Economy.Shields_Available`
* **n9 (Shield Capacity):** `UserProfile.Economy.Max_Shield_Cap`

---

## 3. Presentation Layer (MAUI HUD Injection)

### 3.1 Layout Structure & Formatting
Within the fixed Global HUD layout zone, construct a responsive horizontal flex/grid layout to display the data safely on mobile viewports.

* **Left Anchor:**
  * Render the User Setup navigation icon (`account_circle`).
  * Render **n1** directly adjacent to it, enclosed in a strict circular border shape (utilizing the `Primary` or `Secondary` accent color token).
  
* **Center Telemetry Container & Visual Hierarchy:** Render a horizontally scrollable or tightly justified flex layout containing the four primary economy string formatted pairs utilizing the `Share Tech Mono` secondary font. 
  * **Strict Emphasis Rule:** To focus the user on immediate, actionable data, the active weekly numbers (**n3, n5, n7, n8**) MUST be visually highlighted using the `Primary` text/color token or a heavier font weight. The lifetime/capacity numbers (**n2, n4, n6, n9**) must act as a muted baseline using standard `On-Surface` text colors.
  * **GP Display:** `GP {n2} / ` **`{n3}`**
  * **XP Display:** `XP {n4} / ` **`{n5}`**
  * **SP Display:** `SP {n6} / ` **`{n7}`**
  * **Shields Display:** `Shields ` **`{n8}`** ` / {n9}`

* **Right Anchor:**
  * Render the Notifications navigation icon (`notifications`) pinned to the far right.

### 3.2 Visual & Reactivity State
* Ensure all bindings update asynchronously (using MVVM `INotifyPropertyChanged` patterns) so that if a background habit log changes the week's XP or GP, the HUD reflects it instantly without blocking the UI thread.

---

## 4. Test-Driven Development (TDD) Invariants

* **Aggregation Math Verification:** Write robust unit tests for the HUD Query Handler using mock `UserProfile` and `Week` data.
  * Explicitly assert that the **n3** (Weekly GP) calculation accurately averages multiple active goals.
  * Explicitly assert that the **n5** (Weekly XP) calculation accurately sums multiple active goals.
* **ViewModel Binding Verification:** Assert that the specific `n3`, `n5`, `n7`, and `n8` properties are isolated as dedicated bindable strings to allow for UI-level color highlighting.